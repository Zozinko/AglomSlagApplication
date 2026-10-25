# Словарь контрактов GraphQL

Формальная схема: [schema.graphql](schema.graphql). Здесь описаны назначение полей и преобразования, которые нужны клиенту и агенту при расширении кода. Во всех существующих input поля обязательны (`!`), включая числовые, булевы и вложенные объекты. Для объектов в ответах нужен selection set. `Float` представляет и C# `float` (хранение), и `double` (расчёт); это не одинаковая точность.

## Единицы и значения

Химический состав в seed передаётся в процентах: `fe=58.5`, `s=0.028`, а не `0.585` и `0.00028`. Имена `ash*Fraction` не означают, что сервер делит число на 100: значения передаются в DLL напрямую, в примере `ashCaOFraction=7.8`. Температура — °C. `materialParts.value` — доля 0..1. Для `totalSInOre` комментарий DTO указывает кг/т чугуна, для вязкости используются пуазы. Прочие единицы расходов/выходов сервер явно не формализует; не вводить пересчёт без согласования предметного контракта библиотек.

Все примеры — технические данные для проверки интеграции. Даже успешный GraphQL-ответ не подтверждает физическую допустимость состава: сервер не проверяет сумму компонентов, диапазон всех процентов и область применимости математических моделей.

## Сохранение: ComponentGuideInput

Используется в `addComponentGuide` и `updateComponentGuide`. Пример: [AddComponentGuide.variables.json](examples/AddComponentGuide.variables.json).

| Поле | Тип | Смысл |
|---|---|---|
| `componentName` | `String!` | имя материала |
| `userId` | `Int!` | метка пользователя; не проверяется по Auth |
| `feContent` | `Float!` | Fe |
| `siO2Content` | `Float!` | SiO2 |
| `al2O3Content` | `Float!` | Al2O3 |
| `caOContent` | `Float!` | CaO |
| `mgOContent` | `Float!` | MgO |
| `sContent` | `Float!` | S |
| `mnOContent` | `Float!` | MnO |
| `tiO2Content` | `Float!` | TiO2 |

Output `ComponentGuide` дополнительно содержит `componentId` и навигацию `componentVariants` (список текущими запросами справочника не загружается). Сохранение принимает весь объект; update не является частичным PATCH.

## Сохранение: ComponentVariantInput

Элемент отдельного аргумента `components` в add/update варианта. Поля: `componentId: Int!`, `componentName: String!`, `consumption: Float!` и все восемь полей состава `*Content` из предыдущей таблицы. `userId` здесь отсутствует.

`componentId` должен существовать в справочнике и не повторяться внутри одного варианта. Имя, расход и состав сохраняются из input, а не читаются автоматически из справочника. Output дополнительно содержит `variantId`, `calcVariant`, `componentGuide`; последняя связь не загружается обычным чтением вариантов и при её выборе возможна non-null ошибка. Выходные примеры поэтому используют скалярные поля компонента.

## Сохранение: CalcVariantInput

Используется add/update вариантами. Пример: [AddCalcVariant.variables.json](examples/AddCalcVariant.variables.json). Все перечисленные числовые показатели — `Float!`, кроме явно указанных типов.

| Группа | Поля | Назначение |
|---|---|---|
| Идентификация | `bfId: Int!`, `variantName: String!` | существующая печь и имя варианта |
| Дата и источник | `calcDate: DateTime!`, `isImport: Boolean!` | дата расчёта в UTC ISO 8601 и признак импорта; специальной импортной обработки нет |
| Кокс | `cokeConsumption`, `cokeSContent`, `cokeAshAmount` | расход, сера, количество золы |
| Зола кокса | `cokeAshCaOContent`, `cokeAshSiO2Content`, `cokeAshAl2O3Content`, `cokeAshMgOContent` | состав золы |
| Шлак | `slagCaOContent`, `slagSiO2Content`, `slagTiO2Content`, `slagAl2O3Content`, `slagMgOContent` | состав шлака |
| Чугун | `ciTemperature`, `ciSiContent`, `ciSContent`, `ciMnContent`, `ciCContent`, `ciTiContent`, `ciCrContent` | температура и состав чугуна |

Input не содержит `variantId`, `saveTime`, `parentId` и `components`. ID для update передаётся отдельным аргументом, компоненты — отдельным списком `components`. В output `CalcVariant` дополнительно присутствуют `variantId`, `saveTime`, `blastFurnace`, `parentId`, `parent`, `children`, `componentVariants`. `saveTime` сейчас вычисляется при создании C#-объекта и не хранится в БД. `parent`/`children` не загружаются явно и не редактируются текущим API. Для update не выбирать `blastFurnace` в ответе: связь не загружена, возможен `HC0018` уже после сохранения; читать её последующим `calcVariantById`.

## Расчёт: InputDataModelInput (шлаковый режим)

Не совпадает с DTO хранения. Поля верхнего уровня: `coke: InputDataCokeInput!`, `iron: InputDataIronInput!`, `slag: InputDataSlagInput!`, `components: [MaterialInput!]!`. Пример: [CalculateSlagMode.variables.json](examples/CalculateSlagMode.variables.json).

| Объект | Все поля (числа — `Float!`) |
|---|---|
| `coke` | `consumption`, `sulfur`, `ashAmount`, `ashCaOFraction`, `ashSiO2Fraction`, `ashAl2O3Fraction`, `ashMgOFraction` |
| `iron` | `si`, `s`, `mn`, `c`, `ti`, `cr`, `temp` |
| `slag` | `caO`, `siO2`, `tiO2`, `al2O3`, `mgO` |
| каждый `components[]` (`MaterialInput`) | `sourcename: String!`, `consumption`, `fe`, `siO2`, `al2O3`, `caO`, `mgO`, `s`, `mnO`, `tiO2` |

Написание `sourcename` слитное и полностью в нижнем регистре. В input расчёта нет `componentId`, `bfId`, `userId` или `variantId`: можно считать без сохранения в БД. Список материалов должен быть непустым, суммарный `consumption` — больше нуля. Каждый индивидуальный отрицательный расход отдельно не запрещается текущей валидацией; клиенту нельзя считать это подтверждением корректности данных.

### Преобразование сохранённого варианта в расчёт

Серверного endpoint «рассчитать по ID» нет. После `calcVariantById` клиент формирует input:

| Источник (`CalcVariant`) | Поле расчётного input |
|---|---|
| `cokeConsumption` | `coke.consumption` |
| `cokeSContent` | `coke.sulfur` |
| `cokeAshAmount` | `coke.ashAmount` |
| `cokeAshCaOContent` | `coke.ashCaOFraction` |
| `cokeAshSiO2Content` | `coke.ashSiO2Fraction` |
| `cokeAshAl2O3Content` | `coke.ashAl2O3Fraction` |
| `cokeAshMgOContent` | `coke.ashMgOFraction` |
| `ciTemperature` | `iron.temp` |
| `ciSiContent`, `ciSContent`, `ciMnContent`, `ciCContent`, `ciTiContent`, `ciCrContent` | соответственно `iron.si`, `iron.s`, `iron.mn`, `iron.c`, `iron.ti`, `iron.cr` |
| `slagCaOContent`, `slagSiO2Content`, `slagTiO2Content`, `slagAl2O3Content`, `slagMgOContent` | соответственно `slag.caO`, `slag.siO2`, `slag.tiO2`, `slag.al2O3`, `slag.mgO` |
| `componentVariants[].componentName` | `components[].sourcename` |
| `componentVariants[].consumption` | `components[].consumption` |
| `componentVariants[].feContent`, `siO2Content`, `al2O3Content`, `caOContent`, `mgOContent`, `sContent`, `mnOContent`, `tiO2Content` | соответственно `components[].fe`, `siO2`, `al2O3`, `caO`, `mgO`, `s`, `mnO`, `tiO2` |

Использовать состав из `componentVariants`, если нужен повтор сохранённого расчёта; актуальный справочник мог быть отредактирован. Для обратного сохранения расчётного DTO дополнительно нужны ID печи и компонентов, дата, имя и `isImport`, которых в DTO расчёта нет.

### OutputSlagMode

Все скалярные результаты — `Float!`:

| Поля | Назначение / источник в адаптере |
|---|---|
| `slagBasicity1` | CaO / SiO2 из `BaseOsnovnost1` |
| `slagBasicity2` | (CaO + MgO) / SiO2 |
| `slagBasicity3` | (CaO + MgO) / (SiO2 + Al2O3) |
| `slagBasicityKulikov` | основность по Куликову из SlagSolver |
| `slagOut`, `balSlagMass` | оба из `MassOfOxSlagSum` UBF |
| `caOBalSlagMass` | выход по балансу CaO из `UdWeight` |
| `materialCons` | расход рудных материалов из `RashodRudMat` |
| `totalMat` | сумма входных `consumption` |
| `viscosity1400`, `viscosity1450`, `viscosity1500`, `viscosity1550` | вязкость при указанной температуре |
| `temp7Puaz`, `slagTemperature25Puaz` | температуры при вязкости 7 и 25 пуаз |
| `gradient725`, `gradient14001500` | градиенты из SlagSolver |
| `slagTemperature` | из `UBF_Functions.GetSlagTemparature` для входного чугуна |
| `currSlagViscosity` | вязкость при вычисленной температуре шлака |
| `totalSInOre` | масса серы, вносимой в печь, кг/т чугуна, из `GetTotalRuda_S` |
| `sActivity`, `sDistribution` | активность и коэффициент распределения серы из UBF |
| `sContentInCastIron` | входное содержание S чугуна |
| `castIronTemp` | текущее значение 0: адаптер не заполняет это поле |

`materialParts: [[KeyValuePairOfStringAndDouble!]!]!` — список словарей по одному материалу; выбирать `materialParts { key value }`, где `key` — имя материала, `value` — доля расхода. Форма JSON:

```json
{"materialParts":[[{"key":"Материал A","value":0.6}],[{"key":"Материал B","value":0.4}]]}
```

Список словарей допускает одинаковые имена разных материалов, поэтому нельзя безусловно сворачивать его в один dictionary без решения о суммировании.

## Расчёт: CalculateAglomInput (агломерат)

Пример: [CalculateAglom.variables.json](examples/CalculateAglom.variables.json). Числа во вложенных объектах — `Float!`.

| Объект / поле | Поля и назначение |
|---|---|
| `userId: Int!` | возвращается в `result.id`, не проверяется по базе |
| `createPreset: Boolean!` | обязателен, но пока игнорируется |
| `compositionOfCokeAsh: CompositionOfCokeAshInput!` | `fe`, `caO`, `siO2`, `al2O3`, `mgO`, `p` — состав золы |
| `compositionOfCoke: CompositionOfCokeInput!` | `consumption`, `percentAsh`, `percentSulfur`, `percentVolatiles` — расход и показатели коксика |
| `fluxAdditions: FluxAdditionsInput!` | обязательные `limestone` и `dolomite` типа `FluxCompositionInput!`; у каждого `caO`, `siO2`, `al2O3`, `mgO` |
| `chargeComponents: [ChargeComponentInput!]!` | каждый компонент: `name: String!`, `weight`, `wet`, `pmpp`, `fe`, `feO`, `caO`, `siO2`, `mgO`, `al2O3`, `tiO2`, `s`, `p`, `cr`, `zn`, `mnO` |
| `aglomParameters: AglomParametersInput!` | `basicity`, `feOinAgl`, `fluxType: FluxType!` |

`wet` и `pmpp` передаются напрямую как показатели влажности и потерь при прокаливании; адаптер их не преобразует. При необходимости уточнения трактовки обращаться к контракту готовой библиотеки. `FluxType` в SDL имеет ровно два значения: `LIMESTONE`, `DOLOMITE`. В variables JSON enum передаётся строкой; в inline GraphQL — без кавычек. Оба состава флюсов обязательны независимо от выбранного типа.

Агломерационный input использует `name/weight`, шлаковый — `sourcename/consumption`, а хранилище — `componentName/*Content`. Нельзя передать один DTO вместо другого только переименованием верхнего объекта.

### AglomCalculationResult и AglomComponentResult

Результат: `id: Int!`, `message: String!`, `components: [AglomComponentResult!]!`. `id` — эхо `userId`, не ID расчёта. Сообщение успешного адаптера: «Расчёт выполнен». Каждая отчётная строка содержит:

| Поля | Назначение |
|---|---|
| `componentName: String!` | название строки отчёта |
| `reportComponentOfCharge` | отчётный показатель компонента шихты |
| `reportFe`, `reportS`, `reportP`, `reportFeO`, `reportFe2O3`, `reportCaO`, `reportSiO2`, `reportAl2O3`, `reportMgO`, `reportMnO`, `reportTiO2`, `reportZn` | отчётные значения соответствующих элементов/оксидов |
| `reportPMPP` | отчётный показатель ПМПП |
| `reportCaO_SiO2` | отчётный показатель CaO/SiO2 (с подчёркиванием в имени) |
| `reportOxideSum` | отчётная сумма оксидов |

Все `report*` — `Float!`. Адаптер только копирует результаты DLL; не следует трактовать каждую промежуточную строку как нормированный процент конечного продукта. В проверенном примере библиотека вернула строки «Шихта», «Известняк», «Доломит», «Коксик», «Зола коксика», «Итог». Это наблюдение конкретного примера, а не жёстко заданный сервером список.
