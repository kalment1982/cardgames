#!/usr/bin/env python3
"""
LLM技巧审核工具
使用LLM审核和优化Tractor_Skills_And_Parameters.md中的技巧定义
"""
import os
import yaml
from openai import OpenAI


class SkillReviewer:
    """技巧审核器"""

    def __init__(self, config_path: str = "config.yaml"):
        # 加载配置
        with open(config_path, 'r', encoding='utf-8') as f:
            self.config = yaml.safe_load(f)

        llm_config = self.config['llm']

        # 解析环境变量
        self.api_key = self._resolve_env(llm_config['api_key'])
        self.api_base = self._resolve_env(llm_config['api_base'])
        self.model = llm_config['model']

        # 初始化客户端
        self.client = OpenAI(
            api_key=self.api_key,
            base_url=self.api_base
        )

        print(f"LLM Skill Reviewer initialized:")
        print(f"  API Base: {self.api_base}")
        print(f"  Model: {self.model}")
        print()

    def _resolve_env(self, value: str) -> str:
        """解析环境变量"""
        if isinstance(value, str) and value.startswith('${') and value.endswith('}'):
            var_name = value[2:-1]
            env_value = os.getenv(var_name)
            if env_value is None:
                raise ValueError(f"环境变量 {var_name} 未设置！")
            return env_value
        return value

    def review_skills_document(self, doc_path: str) -> dict:
        """审核技巧文档"""
        print(f"正在审核文档: {doc_path}")
        print()

        # 读取文档
        with open(doc_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # 构造审核prompt
        prompt = f"""你是拖拉机80分游戏的专家。请审核以下技巧文档，提供专业意见。

# 文档内容

{content}

# 审核任务

请从以下几个方面审核这份技巧文档：

## 1. 技巧正确性
- 检查每个技巧是否符合拖拉机游戏规则
- 指出任何错误或不准确的描述
- 评估技巧的实用性和重要性

## 2. 完整性
- 是否遗漏了重要的游戏技巧？
- 哪些技巧需要补充更多细节？
- 是否需要增加新的技巧分类？

## 3. 参数设置
- 评估各个参数的默认值是否合理
- 哪些参数的权重可能需要调整？
- 参数之间的相对关系是否合理？

## 4. 优先级建议
- P0/P1/P2的优先级划分是否合理？
- 哪些技巧应该优先实现？
- 训练方案是否可行？

## 5. 改进建议
- 提供具体的改进建议
- 建议新增哪些技巧或参数
- 如何优化现有的技巧描述

# 输出格式

请按以下格式输出审核结果：

```json
{{
  "overall_assessment": "总体评价（1-2段）",
  "correctness": {{
    "score": 0-10,
    "issues": ["问题1", "问题2"],
    "suggestions": ["建议1", "建议2"]
  }},
  "completeness": {{
    "score": 0-10,
    "missing_skills": ["遗漏的技巧1", "遗漏的技巧2"],
    "suggestions": ["建议1", "建议2"]
  }},
  "parameters": {{
    "score": 0-10,
    "adjustments": [
      {{"param": "参数名", "current": 0.85, "suggested": 0.90, "reason": "原因"}},
    ],
    "suggestions": ["建议1", "建议2"]
  }},
  "priority": {{
    "score": 0-10,
    "adjustments": ["调整建议1", "调整建议2"],
    "suggestions": ["建议1", "建议2"]
  }},
  "improvements": {{
    "new_skills": [
      {{"name": "技巧名", "description": "描述", "priority": "P0/P1/P2"}},
    ],
    "new_parameters": [
      {{"name": "参数名", "value": 0.75, "description": "描述"}},
    ],
    "general_suggestions": ["建议1", "建议2"]
  }}
}}
```

请提供详细、专业的审核意见。
"""

        # 调用LLM
        print("正在调用LLM进行审核...")
        response = self.client.chat.completions.create(
            model=self.model,
            messages=[{"role": "user", "content": prompt}],
            max_tokens=4096,
            temperature=0.7
        )

        result_text = response.choices[0].message.content
        print("✓ 审核完成！")
        print()

        return result_text

    def review_specific_skill(self, skill_name: str, skill_description: str) -> str:
        """审核特定技巧"""
        prompt = f"""你是拖拉机80分游戏的专家。请审核以下技巧：

# 技巧名称
{skill_name}

# 技巧描述
{skill_description}

# 审核要求

1. **正确性**: 这个技巧是否正确？是否符合游戏规则？
2. **完整性**: 描述是否完整？是否遗漏重要细节？
3. **实用性**: 这个技巧在实战中有多重要？（1-10分）
4. **参数建议**: 如果需要参数化，建议哪些参数？默认值多少？
5. **改进建议**: 如何改进这个技巧的描述？

请提供详细的审核意见。
"""

        response = self.client.chat.completions.create(
            model=self.model,
            messages=[{"role": "user", "content": prompt}],
            max_tokens=2048,
            temperature=0.7
        )

        return response.choices[0].message.content

    def suggest_new_skills(self) -> str:
        """让LLM建议新的技巧"""
        prompt = """你是拖拉机80分游戏的专家。请基于你对游戏的理解，建议一些可能被遗漏的重要技巧。

# 背景

拖拉机80分是一个4人2v2的扑克牌游戏，包含：
- 叫牌阶段：争夺庄家
- 扣底阶段：庄家扣8张底牌
- 出牌阶段：13轮出牌，争夺分数

# 已有技巧分类

1. 基础技巧：跟牌原则（对家赢/能赢/不能赢）
2. 庄家技巧：扣底、保底、先手策略
3. 闲家技巧：毙牌、抠底、配合
4. 高级技巧：记牌、甩牌、局势判断、对子拖拉机

# 任务

请建议5-10个可能被遗漏的重要技巧，包括：
- 技巧名称
- 技巧描述
- 重要性（1-10）
- 建议的参数名和默认值
- 应用场景

请提供详细的建议。
"""

        response = self.client.chat.completions.create(
            model=self.model,
            messages=[{"role": "user", "content": prompt}],
            max_tokens=3072,
            temperature=0.8
        )

        return response.choices[0].message.content

    def optimize_parameters(self, current_params: dict) -> str:
        """优化参数设置"""
        params_str = "\n".join([f"{k}: {v}" for k, v in current_params.items()])

        prompt = f"""你是拖拉机80分游戏的专家。请审核以下参数设置，并提供优化建议。

# 当前参数

{params_str}

# 审核要求

1. **合理性**: 每个参数的值是否合理？
2. **相对关系**: 参数之间的相对大小是否合理？
3. **优化建议**: 哪些参数应该调高/调低？为什么？
4. **新增参数**: 建议新增哪些参数？

请提供详细的优化建议，包括：
- 每个参数的建议值
- 调整的理由
- 预期的效果

输出格式：
```json
{{
  "adjustments": [
    {{"param": "参数名", "current": 0.85, "suggested": 0.90, "reason": "原因"}},
  ],
  "new_params": [
    {{"name": "新参数名", "value": 0.75, "reason": "原因"}},
  ],
  "overall_suggestions": ["建议1", "建议2"]
}}
```
"""

        response = self.client.chat.completions.create(
            model=self.model,
            messages=[{"role": "user", "content": prompt}],
            max_tokens=2048,
            temperature=0.7
        )

        return response.choices[0].message.content


def main():
    """主函数"""
    import argparse

    parser = argparse.ArgumentParser(description="LLM技巧审核工具")
    parser.add_argument("--config", type=str, default="config.yaml", help="配置文件")
    parser.add_argument("--doc", type=str,
                       default="../doc/Tractor_Skills_And_Parameters.md",
                       help="技巧文档路径")
    parser.add_argument("--action", type=str, default="review",
                       choices=["review", "suggest", "optimize"],
                       help="操作类型")
    parser.add_argument("--output", type=str, default="skill_review_result.md",
                       help="输出文件")

    args = parser.parse_args()

    # 创建审核器
    reviewer = SkillReviewer(args.config)

    if args.action == "review":
        # 审核整个文档
        result = reviewer.review_skills_document(args.doc)

        # 保存结果
        with open(args.output, 'w', encoding='utf-8') as f:
            f.write("# 拖拉机技巧文档审核结果\n\n")
            f.write(f"审核时间: {__import__('datetime').datetime.now()}\n\n")
            f.write("---\n\n")
            f.write(result)

        print(f"✓ 审核结果已保存到: {args.output}")

    elif args.action == "suggest":
        # 建议新技巧
        result = reviewer.suggest_new_skills()

        with open(args.output, 'w', encoding='utf-8') as f:
            f.write("# LLM建议的新技巧\n\n")
            f.write(result)

        print(f"✓ 建议已保存到: {args.output}")

    elif args.action == "optimize":
        # 优化参数
        # 读取当前参数
        current_params = {
            "PartnerWinning_GivePointsPriority": 0.85,
            "PartnerWinning_DiscardSmallPriority": 0.80,
            "PartnerWinning_AvoidTrumpPriority": 0.70,
            "PartnerWinning_KeepPairsPriority": 0.60,
            "WinAttempt_UseMinimalCardsPriority": 0.75,
            "WinAttempt_PreserveControlPriority": 0.70,
            "WinAttempt_NextLeadValueWeight": 0.65,
            "CannotWin_DiscardSmallPriority": 0.85,
            "CannotWin_AvoidPointsPriority": 0.80,
            "CannotWin_AvoidTrumpPriority": 0.75,
            "CannotWin_PreserveLongSuitPriority": 0.60,
            "BottomProtection_Alertness": 0.70,
            "BottomProtection_AvoidShortSuitPriority": 0.75,
            "BottomProtection_PreferTrumpPriority": 0.65,
            "BottomProtection_HelpPartnerPriority": 0.60,
            "Lead_BigCardControlPriority": 0.60,
            "Lead_ClearShortSuitPriority": 0.55,
            "Lead_ProbeOpponentPriority": 0.50,
            "Lead_LeadingSafetyBias": 0.65,
        }

        result = reviewer.optimize_parameters(current_params)

        with open(args.output, 'w', encoding='utf-8') as f:
            f.write("# 参数优化建议\n\n")
            f.write(result)

        print(f"✓ 优化建议已保存到: {args.output}")


if __name__ == "__main__":
    main()
