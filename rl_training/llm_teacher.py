"""
LLM Teacher - 使用云端LLM生成专家数据
支持自定义环境变量名配置API密钥和URL
"""
import os
import json
import yaml
import re
from typing import List, Dict, Any, Optional
from game_engine import TractorGame, Card, Suit, Rank, GamePhase
from enhanced_state_encoder import EnhancedStateEncoder
import time


class LLMTeacher:
    def __init__(self, config_path: str = "config.yaml"):
        """初始化LLM Teacher"""
        # 加载配置
        with open(config_path, 'r', encoding='utf-8') as f:
            self.config = yaml.safe_load(f)

        self.llm_config = self.config['llm']
        self.encoder = EnhancedStateEncoder()

        # 解析环境变量
        self.api_key = self._resolve_env_var(self.llm_config['api_key'])
        self.api_base = self._resolve_env_var(self.llm_config['api_base'])
        self.model = self.llm_config['model']

        # 初始化API客户端
        self.client = self._init_client()

        print(f"LLM Teacher initialized:")
        print(f"  API Type: {self.llm_config['api_type']}")
        print(f"  API Base: {self.api_base}")
        print(f"  Model: {self.model}")

    def _resolve_env_var(self, value: str) -> str:
        """解析环境变量引用 ${VAR_NAME}"""
        if isinstance(value, str) and value.startswith('${') and value.endswith('}'):
            var_name = value[2:-1]
            env_value = os.getenv(var_name)
            if env_value is None:
                raise ValueError(f"Environment variable {var_name} not set!")
            return env_value
        return value

    def _init_client(self):
        """初始化API客户端"""
        api_type = self.llm_config['api_type']

        if api_type == "openai":
            from openai import OpenAI
            return OpenAI(
                api_key=self.api_key,
                base_url=self.api_base
            )
        elif api_type == "anthropic":
            from anthropic import Anthropic
            return Anthropic(
                api_key=self.api_key,
                base_url=self.api_base
            )
        else:
            raise ValueError(f"Unsupported API type: {api_type}")

    def generate_expert_action(self, game: TractorGame, player: int) -> List[Card]:
        """让LLM分析游戏状态并给出最佳出牌"""
        # 构造prompt
        prompt = self._build_prompt(game, player)

        # 调用LLM API
        try:
            response = self._call_llm(prompt)
            action = self._parse_action(response, game, player)
            return action
        except Exception as e:
            print(f"LLM call failed: {e}, using random action")
            # 如果LLM调用失败，返回随机合法动作
            legal_actions = game.get_legal_actions(player)
            return legal_actions[0] if legal_actions else []

    def _call_llm(self, prompt: str) -> str:
        """调用LLM API"""
        api_type = self.llm_config['api_type']

        if api_type == "openai":
            response = self.client.chat.completions.create(
                model=self.model,
                messages=[{"role": "user", "content": prompt}],
                max_tokens=self.llm_config['max_tokens'],
                temperature=self.llm_config['temperature'],
                timeout=self.llm_config['timeout']
            )
            return response.choices[0].message.content

        elif api_type == "anthropic":
            response = self.client.messages.create(
                model=self.model,
                max_tokens=self.llm_config['max_tokens'],
                messages=[{"role": "user", "content": prompt}],
                temperature=self.llm_config['temperature']
            )
            return response.content[0].text

        else:
            raise ValueError(f"Unsupported API type: {api_type}")

    def _build_prompt(self, game: TractorGame, player: int) -> str:
        """构造给LLM的prompt"""
        state = game.state
        legal_actions = game.get_legal_actions(player)

        # 格式化手牌
        hand_str = self._format_cards(state.hands[player])

        # 格式化当前牌桌
        trick_str = ""
        if state.current_trick:
            for p, cards in state.current_trick:
                if cards:
                    trick_str += f"  玩家{p}: {self._format_cards(cards)}\n"

        # 格式化合法出牌选项
        actions_str = ""
        for i, action in enumerate(legal_actions[:10]):  # 只显示前10个选项
            actions_str += f"  选项{i}: {self._format_cards(action)}\n"

        trump_rank = getattr(state, 'trump_rank', state.level_rank)

        current_round = len(getattr(state, 'tricks_history', state.played_tricks)) + 1

        prompt = f"""你是拖拉机80分游戏的专家。请分析当前局面并选择最佳出牌。

**游戏状态:**
- 当前玩家: {player} ({'庄家方' if player in [0, 2] else '闲家方'})
- 主牌花色: {state.trump_suit.name if state.trump_suit else 'None'}
- 主牌等级: {trump_rank}
- 当前轮次: {current_round}/13

**手牌:**
{hand_str}

**当前牌桌:**
{trick_str if trick_str else "  (首家出牌)"}

**合法出牌选项:**
{actions_str}

**请分析并选择最佳出牌。输出格式:**
```json
{{
  "reasoning": "你的分析过程（考虑：当前局势、各选项优劣、配合策略）",
  "best_action": 0,
  "confidence": 0.85
}}
```
"""
        return prompt

    def _format_cards(self, cards: List[Card]) -> str:
        """格式化牌列表为字符串"""
        if not cards:
            return "(无)"

        suit_names = {
            Suit.SPADE: "♠",
            Suit.HEART: "♥",
            Suit.CLUB: "♣",
            Suit.DIAMOND: "♦",
            Suit.JOKER: "王"
        }

        rank_names = {
            Rank.TWO: "2", Rank.THREE: "3", Rank.FOUR: "4",
            Rank.FIVE: "5", Rank.SIX: "6", Rank.SEVEN: "7",
            Rank.EIGHT: "8", Rank.NINE: "9", Rank.TEN: "10",
            Rank.JACK: "J", Rank.QUEEN: "Q", Rank.KING: "K",
            Rank.ACE: "A", Rank.SMALL_JOKER: "小王", Rank.BIG_JOKER: "大王"
        }

        result = []
        for card in cards:
            suit = suit_names.get(card.suit, "?")
            rank = rank_names.get(card.rank, "?")
            result.append(f"{suit}{rank}")

        return " ".join(result)

    def _parse_action(self, response: str, game: TractorGame, player: int) -> List[Card]:
        """解析LLM的响应，提取选择的动作"""
        try:
            # 提取JSON部分
            json_match = re.search(r'```json\s*(\{.*?\})\s*```', response, re.DOTALL)
            if json_match:
                json_str = json_match.group(1)
                data = json.loads(json_str)
                action_idx = int(data.get('best_action', 0))

                legal_actions = game.get_legal_actions(player)
                if 0 <= action_idx < len(legal_actions):
                    return legal_actions[action_idx]

        except Exception as e:
            print(f"Failed to parse LLM response: {e}")

        # 解析失败，返回第一个合法动作
        legal_actions = game.get_legal_actions(player)
        return legal_actions[0] if legal_actions else []

    def collect_expert_dataset(self, num_games: int = None, resume_from: int = 0) -> List[Dict]:
        """收集专家数据集"""
        if num_games is None:
            num_games = self.config['data_generation']['num_games']

        save_path = self.config['data_generation']['save_path']
        batch_size = self.config['data_generation']['batch_size']

        dataset = []

        # 如果有已保存的数据，加载它
        if resume_from > 0 and os.path.exists(save_path):
            with open(save_path, 'r') as f:
                dataset = json.load(f)
            print(f"Resumed from {resume_from} games, loaded {len(dataset)} samples")

        print(f"Collecting expert dataset: {num_games} games")
        print(f"Starting from game {resume_from}")

        for game_idx in range(resume_from, num_games):
            game = TractorGame()
            game.reset()
            game.state.phase = GamePhase.PLAYING
            game.state.current_player = 0

            game_data = []

            while not game.is_terminal():
                player = game.current_player

                # 让LLM决策
                action = self.generate_expert_action(game, player)

                # 记录状态-动作对
                state_vector = self.encoder.encode_state(game.state, player)
                action_idx = self._action_to_index(action, game, player)

                game_data.append({
                    'state': state_vector.tolist(),
                    'action': action_idx,
                    'player': player
                })

                # 执行动作
                game.step(player, action)

            dataset.extend(game_data)

            # 定期保存
            if (game_idx + 1) % batch_size == 0:
                with open(save_path, 'w') as f:
                    json.dump(dataset, f)
                print(f"Progress: {game_idx + 1}/{num_games} games, {len(dataset)} samples")

                # 休息一下，避免API限流
                time.sleep(1)

        # 最终保存
        os.makedirs(os.path.dirname(save_path), exist_ok=True)
        with open(save_path, 'w') as f:
            json.dump(dataset, f)

        print(f"Dataset collection complete!")
        print(f"Total samples: {len(dataset)}")
        print(f"Saved to: {save_path}")

        return dataset

    def _action_to_index(self, action: List[Card], game: TractorGame, player: int) -> int:
        """将动作转换为索引"""
        legal_actions = game.get_legal_actions(player)
        for i, legal_action in enumerate(legal_actions):
            if self._cards_equal(action, legal_action):
                return i
        return 0  # 默认返回第一个合法动作

    def _cards_equal(self, cards1: List[Card], cards2: List[Card]) -> bool:
        """比较两组牌是否相同"""
        if len(cards1) != len(cards2):
            return False

        # 简单比较：转换为字符串排序后比较
        str1 = sorted([f"{c.suit.value}_{c.rank.value}" for c in cards1])
        str2 = sorted([f"{c.suit.value}_{c.rank.value}" for c in cards2])

        return str1 == str2


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="LLM Teacher - Generate expert dataset")
    parser.add_argument("--config", type=str, default="config.yaml", help="Config file path")
    parser.add_argument("--generate", action="store_true", help="Generate expert dataset")
    parser.add_argument("--num-games", type=int, default=None, help="Number of games to generate")
    parser.add_argument("--resume-from", type=int, default=0, help="Resume from game index")

    args = parser.parse_args()

    teacher = LLMTeacher(args.config)

    if args.generate:
        teacher.collect_expert_dataset(
            num_games=args.num_games,
            resume_from=args.resume_from
        )
    else:
        print("Use --generate to start collecting expert dataset")
        print("Example: python llm_teacher.py --generate --num-games 100")
