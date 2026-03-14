#!/usr/bin/env python3
"""
测试LLM API连接
"""
import os
import yaml

def test_llm_connection():
    """测试LLM API连接"""
    print("=" * 60)
    print("测试LLM API连接")
    print("=" * 60)
    print()

    # 加载配置
    with open('config.yaml', 'r') as f:
        config = yaml.safe_load(f)

    llm_config = config['llm']

    # 解析环境变量
    def resolve_env(value):
        if isinstance(value, str) and value.startswith('${') and value.endswith('}'):
            var_name = value[2:-1]
            env_value = os.getenv(var_name)
            if env_value is None:
                raise ValueError(f"环境变量 {var_name} 未设置！")
            return env_value
        return value

    api_key = resolve_env(llm_config['api_key'])
    api_base = resolve_env(llm_config['api_base'])
    model = llm_config['model']
    api_type = llm_config['api_type']

    print(f"配置信息:")
    print(f"  API类型: {api_type}")
    print(f"  API Base: {api_base}")
    print(f"  API Key: {api_key[:20]}...")
    print(f"  模型: {model}")
    print()

    # 测试连接
    print("正在测试API连接...")
    try:
        if api_type == "openai":
            from openai import OpenAI
            client = OpenAI(
                api_key=api_key,
                base_url=api_base
            )

            # 发送简单测试请求
            response = client.chat.completions.create(
                model=model,
                max_tokens=100,
                messages=[{
                    "role": "user",
                    "content": "请用一句话介绍拖拉机80分游戏。"
                }]
            )

            print("✓ API连接成功！")
            print()
            print("测试响应:")
            print(f"  {response.choices[0].message.content}")

        elif api_type == "anthropic":
            from anthropic import Anthropic
            client = Anthropic(
                api_key=api_key,
                base_url=api_base
            )

            # 发送简单测试请求
            response = client.messages.create(
                model=model,
                max_tokens=100,
                messages=[{
                    "role": "user",
                    "content": "请用一句话介绍拖拉机80分游戏。"
                }]
            )

            print("✓ API连接成功！")
            print()
            print("测试响应:")
            print(f"  {response.content[0].text}")

        else:
            raise ValueError(f"不支持的API类型: {api_type}")

        print()
        print("=" * 60)
        print("✓ LLM配置正确，可以开始生成专家数据！")
        print("=" * 60)
        return True

    except Exception as e:
        print(f"✗ API连接失败: {e}")
        print()
        print("请检查:")
        print("  1. 环境变量是否正确设置")
        print("  2. API密钥是否有效")
        print("  3. API Base URL是否正确")
        print("  4. 网络连接是否正常")
        return False


if __name__ == "__main__":
    import sys
    success = test_llm_connection()
    sys.exit(0 if success else 1)
