# 边缘精神疾病 / Rim Mental Disorders

为边缘世界新增大量精神疾病，同时提供副作用和大量增益。

Adds a large collection of mental disorders to RimWorld, each with meaningful drawbacks and substantial benefits.

## Trauma framework / 创伤框架

The pawn inspection pane includes one Trauma tab with Trauma records and Possible
disorders sub-tabs. Disorder risks can show all entries or be filtered by severity.
The mod settings provide an overall severity distribution totaling 100%, plus
per-disease weights totaling 100% within each severity.

小人检查面板包含一个“创伤”页签，内部再分为“创伤记录”和“可能形成的疾病”。
疾病风险可显示全部或按严重度筛选；模组设置可调整合计100%的总体严重度权重，
并在每个严重度内调整合计100%的逐疾病权重。

Extension mods can add XML-only trauma types with
`MoreMentalDisorders.TraumaDef` (or the compatible
`MoreMentalDisorders.MentalCauseDef`) and attach
`MoreMentalDisorders.DiseaseAcquisitionExtension` to any `HediffDef`.
The public `MoreMentalDisorders.TraumaAPI` provides `Add`, `Reduce`,
`GetSeverity`, `GetRecords`, and `GetDiseaseRisks`, plus trauma change and
recovery events.

## 主要内容

- 39种精神疾病，分为轻度、中度、重度和极重。
- 每种疾病都有实际属性、心情、行为或病发机制。
- 38种疾病可以由创伤、关系破裂、失眠、疼痛、孤立、药物、灵能事故等经历在游戏中形成。
- 超忆症只能在角色生成时获得。
- 默认初始患病概率为69.5%，可在模组设置中调整；患病后的严重度与具体疾病权重也可分别配置。
- 当前不允许共病，每名角色最多拥有一种本模组精神疾病。
- 心灵敏感度归零、注射思滞血清或植入心灵稳定芯片可以治愈疾病。
- 新增“神经研究”、心灵稳定芯片和海马体切除术。
- 可选支持RimTalk：患病者的疾病、动态状态和社会偏见会加入对话上下文。
- 支持简体中文和英文。

## Features

- 39 mental disorders across mild, moderate, severe, and extreme tiers.
- Every disorder has real stat, mood, behavioral, or episode mechanics.
- 38 disorders can develop from in-game experiences such as trauma, relationship loss, sleep deprivation, pain, isolation, drugs, and psychic incidents.
- Hyperthymesia is congenital only.
- The default initial disorder chance is 69.5% and is configurable, as are the severity distribution and per-disorder weights.
- Comorbidity is currently disabled: each pawn can have at most one disorder from this mod.
- Disorders can be cured by zero psychic sensitivity, a mind-numb serum, or a mind stabilizer chip.
- Adds Neural Research, the mind stabilizer chip, and hippocampectomy.
- Optional RimTalk integration adds disorders, dynamic states, and social prejudice to dialogue context.
- Simplified Chinese and English localization included.

## Requirements

- RimWorld 1.6
- Harmony
- Royalty
- Anomaly is optional
- RimTalk is optional

Source code is under `Source/MoreMentalDisorders`; the compiled assembly is under `1.6/Assemblies`.
