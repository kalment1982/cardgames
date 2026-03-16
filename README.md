# 拖拉机（80分）游戏 - 核心逻辑

## 项目说明

这是拖拉机游戏的核心逻辑实现，使用纯C#编写，不依赖Unity API。

- **目标平台**：Android、iOS、macOS（通过Unity发布）
- **开发模式**：先实现核心逻辑，后集成Unity UI
- **测试框架**：xUnit

## 项目结构

```
tractor/
├── src/
│   ├── Core/
│   │   ├── Models/        # 数据模型
│   │   ├── Rules/         # 规则引擎（待实现）
│   │   ├── AI/            # AI逻辑（待实现）
│   │   └── GameFlow/      # 游戏流程（待实现）
│   └── Tests/             # 单元测试
├── doc/                   # 文档
│   ├── 80分_拖拉机游戏规则文档_v1.6.md
│   └── 测试用例设计_v1.0.md
└── TractorGame.csproj     # 项目配置
```

## 已完成模块

### ✅ Task 1.1: 核心数据模型

**文件：**
- `src/Core/Models/Enums.cs` - 枚举定义（花色、点数、牌型）
- `src/Core/Models/Card.cs` - 卡牌类
- `src/Core/Models/GameConfig.cs` - 游戏配置
- `src/Core/Models/CardComparer.cs` - 卡牌比较器
- `src/Core/Models/CardPattern.cs` - 牌型识别

**测试文件：**
- `src/Tests/CardTests.cs` - 卡牌测试
- `src/Tests/GameConfigTests.cs` - 配置测试
- `src/Tests/CardPatternTests.cs` - 牌型识别测试

**功能：**
- ✅ 表示108张牌（2副扑克）
- ✅ 识别主牌/副牌
- ✅ 比较牌的大小
- ✅ 识别单张、对子、拖拉机
- ✅ 支持断档拖（级牌在中间时）

## 运行测试

### 前置要求
- .NET 6.0 SDK 或更高版本

### 安装依赖
```bash
dotnet restore
```

### 运行测试
```bash
dotnet test
```

### 快速回归
```bash
./scripts/test_fast.sh
```

说明：
- 默认排除 `SelfPlay / Campaign / Benchmark / LongRunning / UI`
- 适合日常修复单点规则、候选生成、评分器后快速验证

### 标准回归
```bash
./scripts/test_standard.sh
```

说明：
- 保留大多数单元测试与集成测试
- 排除 `SelfPlay / Campaign / LongRunning / UI`
- 适合一批改动完成后的常规回归

### 重型评估
以下测试不建议每次修改都跑，适合夜间、合并前或版本验收时执行：
```bash
dotnet test --filter "Category=SelfPlay"
dotnet test --filter "Category=Benchmark"
dotnet test --filter "Category=Campaign"
```

### 运行特定测试
```bash
dotnet test --filter "FullyQualifiedName~CardTests"
```

## 下一步计划

### Sprint 2: 规则引擎（2-3天）
- [ ] Task 2.1: 出牌合法性校验
- [ ] Task 2.2: 跟牌约束检查
- [ ] Task 2.3: 胜负判定逻辑
- [ ] Task 2.4: 甩牌判定
- [ ] Task 2.5: 抠底计分

### Sprint 3: 游戏流程（2-3天）
- [ ] Task 3.1: 发牌逻辑
- [ ] Task 3.2: 亮主/反主
- [ ] Task 3.3: 扣底
- [ ] Task 3.4: 出牌流程
- [ ] Task 3.5: 升级判定

### Sprint 4: AI实现（3-5天）
- [ ] Task 4.1: AI出牌策略
- [ ] Task 4.2: AI跟牌策略
- [ ] Task 4.3: AI扣底策略
- [ ] Task 4.4: AI难度调整

## 集成到Unity

完成核心逻辑后，将代码迁移到Unity项目：

1. 创建Unity项目（Unity 2021.3 LTS+）
2. 将 `src/Core/` 复制到 `Assets/Scripts/Core/`
3. 创建Unity UI层（MonoBehaviour）
4. 连接逻辑层和UI层

## 参考文档

- [游戏规则文档](doc/80分_拖拉机游戏规则文档_v1.6.md)
- [测试用例设计](doc/测试用例设计_v1.0.md)

## 版本记录

- v0.1.0 (2026-03-12): 完成核心数据模型
