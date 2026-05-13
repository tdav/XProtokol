# XProtocol.Tests — Migrate to TUnit and Reach 90% Line Coverage

**Дата:** 2026-05-08
**Статус:** Утверждён (брейншторм)
**Область:** `XProtocol.Tests/` (тестовый проект). Production-код `XProtocol/` не модифицируется.

---

## 1. Цель

1. Перевести существующий тестовый проект `XProtocol.Tests` с MSTest 4 на TUnit 1.43.11.
2. Расширить покрытие тестов так, чтобы line coverage сборки `XProtocol/` был не ниже **90%**.

Работа делится на две фазы (одна спецификация, два независимо коммитимых блока):

- **Phase A** — механическая миграция существующих 11 тестов на TUnit (без изменений семантики и без увеличения покрытия).
- **Phase B** — добавление новых тестов на TUnit до достижения 90% line coverage.

---

## 2. Принятые решения (резюме брейншторма)

| # | Решение |
|---|---------|
| 1 | Целевая сборка для coverage — **только `XProtocol/`**. Console-samples (`Test/`) и сетевые проекты (`TCPClient/`, `TCPServer/`) не покрываются. |
| 2 | Coverage — **только measurement** (без CI-gate, без fail-on-threshold). Цель = достижение 90% работа над тестами, а не build-time enforcement. |
| 3 | Порядок работы — **Phase A → Phase B** (вариант A брейншторма). Сначала миграция, затем расширение. Каждая фаза — самостоятельные коммиты. |
| 4 | Test framework — **TUnit 1.43.11** (последняя стабильная на 2026-05). |
| 5 | Coverage tooling — **`Microsoft.Testing.Extensions.CodeCoverage`** (MTP-native). HTML-отчёт через `dotnet-reportgenerator-globaltool` опционально (вне csproj). |
| 6 | Production-код `XProtocol/` не изменяется. Если для покрытия требуется атрибут `[ExcludeFromCodeCoverage]` — это нарушение, требует отдельного согласования. |

---

## 3. Архитектура

```
XProtocol.Tests/ (изменяется)
├── XProtocol.Tests.csproj                        # Phase A: пакеты и MTP-настройки
├── TestDtos.cs                                   # Phase A: AssemblyFixture → [Before(HookType.Assembly)]
├── RegistrationTests.cs                          # Phase A: миграция 4 тестов
├── RoundtripTests.cs                             # Phase A: миграция 4 тестов
├── StrictCountTests.cs                           # Phase A: миграция 1 теста
├── UnregisteredTypeTests.cs                      # Phase A: миграция 2 тестов
│
├── XPacketTests.cs                # Phase B (new): Parse malformed, AppendValue/GetValueAt edge cases, Encrypt/Decrypt invariants
├── XPacketTypeManagerTests.cs     # Phase B (new): GetTypeFromPacket, GetBytesFor, BuildDescriptors с наследованием
├── XPacketConverterTests.cs       # Phase B (new): null-arg checks для Serialize/Deserialize
└── RijndaelHandlerTests.cs        # Phase B (new): roundtrip разных размеров, corrupted ciphertext
```

```
docs/coverage/                                   # gitignored (опционально)
└── coverage.cobertura.xml                       # выход dotnet test --coverage
```

---

## 4. Phase A — Миграция на TUnit

### 4.1 csproj

`XProtocol.Tests/XProtocol.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>disable</Nullable>
    <LangVersion>latest</LangVersion>
    <OutputType>Exe</OutputType>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.11" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.13.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\XProtocol\XProtocol.csproj" />
  </ItemGroup>
</Project>
```

Удаляются: `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`.

Добавляются: `OutputType=Exe` (TUnit требует executable host для MTP runner) и `TestingPlatformDotnetTestSupport=true` (интеграция `dotnet test` с MTP).

### 4.2 Mapping атрибутов

| MSTest 4 | TUnit |
|----------|-------|
| `[TestClass]` на классе | (не нужен — auto-discovery по `[Test]`) |
| `[TestMethod]` на методе | `[Test]` |
| `[AssemblyInitialize] public static void M(TestContext _)` | `[Before(HookType.Assembly)] public static void M()` (или `Task M()`) |

### 4.3 Mapping ассертов

Все TUnit-ассерты возвращают `Task` и должны быть `await`-нуты. Сами тестовые методы становятся `async Task`.

| MSTest 4 | TUnit |
|----------|-------|
| `Assert.AreEqual(expected, actual)` | `await Assert.That(actual).IsEqualTo(expected)` |
| `Assert.IsNotNull(x)` | `await Assert.That(x).IsNotNull()` |
| `Assert.IsTrue(x)` | `await Assert.That(x).IsTrue()` |
| `Assert.IsFalse(x)` | `await Assert.That(x).IsFalse()` |
| `StringAssert.Contains(str, sub)` | `await Assert.That(str).Contains(sub)` |
| `Assert.ThrowsExactly<T>(() => action)` | `await Assert.That(() => action).ThrowsExactly<T>()` (возвращает `T` для дальнейших `Assert.That(ex.Message)…`) |

### 4.4 Сигнатуры тестов

```csharp
// Было (MSTest)
[TestMethod]
public void Foo() { Assert.AreEqual(1, 1); }

// Стало (TUnit)
[Test]
public async Task Foo() { await Assert.That(1).IsEqualTo(1); }
```

### 4.5 AssemblyFixture (TestDtos.cs)

```csharp
public static class AssemblyFixture
{
    public const XPacketType SimpleDtoType = (XPacketType)100;
    public const XPacketType EmptyDtoType = (XPacketType)101;

    [Before(HookType.Assembly)]
    public static void Init()
    {
        XPacketTypeManager.Register<SimpleDto>(SimpleDtoType, 100, 0);
        XPacketTypeManager.Register<EmptyDto>(EmptyDtoType, 101, 0);
    }
}
```

`[TestClass]` атрибут на `AssemblyFixture` снимается — TUnit hooks не требуют декорации класса.

### 4.6 Verification (Phase A)

- `dotnet build XProtocol.Tests/XProtocol.Tests.csproj` — 0 errors.
- `dotnet test XProtocol.Tests/XProtocol.Tests.csproj` — все 11 тестов проходят на новом runner.
- Diff не затрагивает `XProtocol/`, `Test/`, `TCPClient/`, `TCPServer/`.

---

## 5. Phase B — Расширение покрытия до 90%

### 5.1 Coverage measurement

Команда:

```
dotnet test XProtocol.Tests/XProtocol.Tests.csproj --coverage --coverage-output coverage.cobertura.xml --coverage-output-format cobertura
```

Выход: `coverage.cobertura.xml` рядом с тестовой сборкой. Атрибут `<coverage line-rate="…">` верхнего узла отчёта — целевой показатель `≥ 0.90` для XProtocol assembly.

Опциональный HTML-отчёт (вне csproj):

```
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

### 5.2 Покрытие — конкретные ветки

#### XPacket.cs (новый файл `XPacketTests.cs`)

- `AppendValue(null)` → `ArgumentNullException`.
- `AppendValue(refType)` → `ArgumentException` "Only value types".
- `AppendValue` для значения с marshal size > 255 — пропуск, если нет типа значения с таким размером (документируется как недостижимая ветка).
- `GetValueAt<T>(-1)` / `GetValueAt<T>(Fields.Count)` → `ArgumentOutOfRangeException`.
- `GetValueAt(int, Type)` out-of-range — те же проверки.
- `Parse(null)` → `null`.
- `Parse(packet of length 7)` → `null` (минимальный размер 8).
- `Parse(packet с заголовком, отличным от 0xAFAAAF и 0x95AAFF)` → `null`.
- `Parse` со среднеотрезанным полем (size > доступного) → `null`.
- `Parse` без footer 0xFF 0x00 — `null`.
- `Parse` с правильным заголовком и отсутствующим payload — `null`.
- `EncryptPacket(null)` → `null`.
- `DecryptPacket` для пакета с `Fields.Count != 1` — `null` (через прямой вход через Parse encrypted-style малым числом полей).
- `Encrypt() instance` → `EncryptPacket(this)` — round-trip с не-handshake DTO.

#### XPacketTypeManager.cs (новый файл `XPacketTypeManagerTests.cs`)

- `GetTypeFromPacket` для зарегистрированного `XPacket` (`PacketType=1, Subtype=0`) → `XPacketType.Handshake`.
- `GetTypeFromPacket` для незарегистрированного `(0xFE, 0xFE)` → `XPacketType.Unknown`.
- `GetBytesFor(typeof(UnregisteredDto))` → `InvalidOperationException` с именем типа.
- `BuildDescriptors` через `Register<DerivedDto>` — `DerivedDto` наследует от `BaseDto` с public field. Проверка: `Serialize → Deserialize` восстанавливает все поля иерархии в порядке `MetadataToken`.

#### XPacketConverter.cs (новый файл `XPacketConverterTests.cs`)

- `Serialize<SimpleDto>(null)` → `ArgumentNullException`.
- `Deserialize<SimpleDto>(null)` → `ArgumentNullException`.

#### RijndaelHandler.cs / XProtocolEncryptor.cs (новый файл `RijndaelHandlerTests.cs`)

- Roundtrip `Encrypt → Decrypt` для размеров: 1, 15, 16, 17, 100, 1000 bytes — байты совпадают.
- Roundtrip через `XProtocolEncryptor.Encrypt`/`Decrypt` (тонкая обёртка) — один тест с typical input.
- `Decrypt` коррумпированного буфера (произвольно изменён один байт ciphertext) → `CryptographicException` (ожидаемое поведение PKCS7 padding).

### 5.3 Прогресс по коммитам

Каждый new test file — отдельный коммит:

1. `test: add XPacket malformed-input and edge-case tests`
2. `test: add XPacketTypeManager descriptor and registry tests`
3. `test: add XPacketConverter null-argument tests`
4. `test: add RijndaelHandler roundtrip and corrupted-ciphertext tests`

После каждого commit — измерение coverage. Если 90% достигнуто раньше — оставить незатронутые тесты для следующей итерации/документировать.

### 5.4 Ожидаемые числа

- **До Phase B**: 11 тестов, line coverage XProtocol/ ориентировочно ~50–60% (точное значение замеряется на старте Phase B).
- **После Phase B**: ~39 тестов, line coverage `XProtocol/` ≥ 90%.

---

## 6. Acceptance Criteria

1. ✅ `dotnet build TCPProtocol.sln` — 0 errors после Phase A и Phase B.
2. ✅ `dotnet test XProtocol.Tests/XProtocol.Tests.csproj` — все тесты pass (11 после Phase A; ~39 после Phase B).
3. ✅ Cobertura отчёт показывает line coverage `XProtocol/` ≥ 0.90.
4. ✅ Production-код `XProtocol/` не изменён (`git diff <base>..HEAD -- XProtocol/` пуст).
5. ✅ `Test/Program.cs` sample продолжает работать (`TestNumber=12345, TestDouble=…, TestBoolean=True`).

---

## 7. Out of Scope

- Branch coverage target.
- Покрытие `Test/`, `TCPClient/`, `TCPServer/`.
- Mutation testing, property-based testing, fuzzing.
- Performance benchmarks для сериализации/шифрования.
- CI/CD pipeline.
- `[ExcludeFromCodeCoverage]` в production-коде.
- Code review concerns из предыдущего цикла (concurrency, GetValueAt reflection, encapsulation, redundant `IsLiteral`) — остаются отдельным follow-up.

---

## 8. Известные риски и mitigation

1. **Coverage не достигает 90%** из-за недостижимых веток (например `>255 fields` в `BuildDescriptors` или `>255 bytes` в `AppendValue`). Mitigation: документировать как технически недостижимые в спеке плана; если необходимо — добавить искусственный тест через reflection-emit (out of scope без отдельного согласования).
2. **Корректность TUnit assertion ↔ MSTest mapping** — некоторые сложные ассерты могут потребовать другой формы. Mitigation: при возникновении неоднозначности консультировать TUnit docs ([github.com/thomhurst/TUnit](https://github.com/thomhurst/TUnit)) и обновить spec.
3. **`Microsoft.Testing.Extensions.CodeCoverage` версия** — нужно подтвердить совместимость с TUnit 1.43.11 на этапе плана (не блокер для спецификации).
4. **TUnit 1.x ещё активно эволюционирует** — pin версии 1.43.11 в csproj. Migration на 2.x — отдельный future spec.
5. **Test execution order** — `[Before(HookType.Assembly)]` гарантирует порядок относительно теста только в рамках сборки. Между классами порядок недетерминирован (как и в MSTest), но AssemblyFixture обеспечивает единократную регистрацию до всех тестов.

---

## 9. Зависимости

- Текущий ветка master, HEAD = `9f79f83` (или новее на момент plan-а).
- TUnit 1.43.11 на `nuget.org`.
- Microsoft.Testing.Extensions.CodeCoverage 17.13.0 на `nuget.org`.
- .NET 10 SDK для запуска (уже установлен).
