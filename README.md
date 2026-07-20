# v2rayN — custom fork

<p align="center">
  <a href="#english">English</a> ·
  <a href="#русский">Русский</a>
</p>

<p align="center">
  <a href="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml"><img src="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml/badge.svg" alt="Windows Light build"></a>
  <a href="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml"><img src="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml/badge.svg" alt="Windows Medium build"></a>
  <a href="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml"><img src="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml/badge.svg" alt="Windows Full build"></a>
</p>

> [!NOTE]
> This is a custom fork of [2dust/v2rayN](https://github.com/2dust/v2rayN), not the official upstream repository.

---

<a id="english"></a>

## English

### About

This repository is a custom fork of **v2rayN**, a graphical proxy client based on the upstream project [2dust/v2rayN](https://github.com/2dust/v2rayN). It supports [Xray](https://github.com/XTLS/Xray-core), [sing-box](https://github.com/SagerNet/sing-box) and other compatible proxy cores.

This fork adds custom subscription headers and User-Agent support, fixes or mitigates Windows TUN startup hangs, adds automatic TUN startup retries, and provides three convenient Windows x64 build variants.

**Search keywords:** v2rayN TUN hang fix, Wintun startup retry, custom User-Agent, subscription headers, HWID, `agent_v`, `-tundelay`.

### Differences from the upstream project

| Area | Upstream project | This fork |
|---|---|---|
| Subscription User-Agent | Standard request behavior | Reads `user_agent` from a global `agent_v` file and sends it as the `User-Agent` header |
| Additional subscription headers | Standard headers | Adds `x-hwid`, `x-device-os`, `x-ver-os` and `x-device-model` from `agent_v` |
| Header scope | Standard subscription processing | Applies custom headers to the main URL, additional URLs, proxy requests and fallback requests |
| TUN startup hangs | Standard core startup | Detects failed Xray or sing-box TUN startup and retries it automatically on Windows |
| TUN retry policy | No custom retry mechanism from this fork | Up to 3 attempts with a 5-second delay between attempts |
| Delayed TUN startup | Standard startup behavior | Adds `-tundelay <seconds>`: the selected server starts first without TUN, then TUN is enabled later |
| Failed TUN fallback | Standard behavior | After all retries fail, disables TUN and starts the selected server without TUN |
| Manual TUN disable | Standard reload behavior | Cancels startup observation promptly when TUN is disabled manually |
| Fine tuning | Standard settings | Creates `finetunes.ini` with configurable `TunStartObservationSeconds` in the range 20–300 seconds |
| Diagnostics | Standard logging | Adds detailed core stop, kill, timeout, exit and TUN retry logging |
| Windows builds | Upstream release workflows | Adds separate manual Light, Medium and Full Windows x64 workflows |
| Included custom configuration | Standard build output | Places the configured `agent_v` file next to `v2rayN.exe` in all three custom builds |

### Download custom builds

Open [GitHub Actions](https://github.com/PrtUsr1976/v2rayN/actions), select the required workflow and click **Run workflow**. After the run completes, open it and download the generated artifact.

| Build | Contents | Requirements | Workflow |
|---|---|---|---|
| **Light** | Application and libraries; no bundled .NET; no proxy cores | Install the required .NET Desktop Runtime separately | [Build Windows Light](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml) |
| **Medium** | Application, libraries and bundled .NET; no proxy cores | No separate .NET installation required | [Build Windows Medium](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml) |
| **Full** | Application, bundled .NET and proxy cores | Complete ready-to-use archive | [Build Windows Full](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml) |

All three custom workflows are manual-only, target Windows x64 and include `agent_v` next to `v2rayN.exe`.

### `agent_v` configuration

Example:

```ini
user_agent=Throne/1.1.6
x_hwid=00000000-0000-0000-0000-000000000000
x_device_os=Windows
x_ver_os=10.0.17763
x_device_model=VirtualBox
```

Supported mappings:

| Key | HTTP header |
|---|---|
| `user_agent` | `User-Agent` |
| `x_hwid` | `x-hwid` |
| `x_device_os` | `x-device-os` |
| `x_ver_os` | `x-ver-os` |
| `x_device_model` | `x-device-model` |

The parser supports UTF-8 BOM, CRLF, blank lines, comments beginning with `;` or `#`, whitespace around keys and values, and duplicate keys where the last value wins.

### Delayed and resilient TUN startup

Enable TUN after a delay:

```text
v2rayN.exe -tundelay 30
```

Start without TUN and leave TUN activation to the user:

```text
v2rayN.exe -tundelay 0
```

TUN startup observation is configured in `finetunes.ini`:

```ini
TunStartObservationSeconds=20
```

Valid values are from 20 to 300 seconds. Invalid values are automatically replaced with `20`.

### Links

- [This fork](https://github.com/PrtUsr1976/v2rayN)
- [Custom build workflows](https://github.com/PrtUsr1976/v2rayN/actions)
- [Upstream project](https://github.com/2dust/v2rayN)
- [Upstream documentation](https://github.com/2dust/v2rayN/wiki)

---

<a id="русский"></a>

## Русский

### О проекте

Это модифицированный форк **v2rayN**, графического прокси-клиента на основе родительского проекта [2dust/v2rayN](https://github.com/2dust/v2rayN). Поддерживаются [Xray](https://github.com/XTLS/Xray-core), [sing-box](https://github.com/SagerNet/sing-box) и другие совместимые прокси-ядра.

В форке добавлены собственный User-Agent и дополнительные заголовки запросов подписки, исправлено или минимизировано зависание запуска TUN в Windows, добавлены автоматические повторные попытки запуска TUN и три варианта сборки для Windows x64.

**Ключевые слова для поиска:** v2rayN исправлено зависание TUN, зависает Wintun, повторный запуск TUN, добавлен User-Agent, заголовки подписки, HWID, `agent_v`, `-tundelay`.

### Отличия от родительского проекта

| Область | Родительский проект | Этот форк |
|---|---|---|
| User-Agent подписки | Стандартное поведение запросов | Читает `user_agent` из общего файла `agent_v` и отправляет его в заголовке `User-Agent` |
| Дополнительные заголовки | Стандартный набор заголовков | Добавляет `x-hwid`, `x-device-os`, `x-ver-os` и `x-device-model` из `agent_v` |
| Область применения заголовков | Стандартная обработка подписок | Использует заголовки для основной ссылки, дополнительных ссылок, запросов через прокси и резервных запросов |
| Зависание запуска TUN | Стандартный запуск ядра | Обнаруживает неудачный запуск TUN через Xray или sing-box и автоматически повторяет его в Windows |
| Повторные попытки TUN | Нет механизма, добавленного этим форком | До 3 попыток с паузой 5 секунд между ними |
| Отложенный запуск TUN | Стандартный запуск | Добавлен параметр `-tundelay <секунды>`: сначала запускается сервер без TUN, затем включается TUN |
| Резервный режим | Стандартное поведение | Если все попытки запуска TUN неудачны, TUN отключается и выбранный сервер запускается без него |
| Ручное отключение TUN | Стандартная перезагрузка конфигурации | Наблюдение за запуском немедленно отменяется при ручном отключении TUN |
| Тонкая настройка | Стандартные настройки | Создаётся `finetunes.ini` с параметром `TunStartObservationSeconds` в диапазоне 20–300 секунд |
| Диагностика | Стандартный журнал | Добавлено подробное журналирование остановки ядра, завершения процесса, тайм-аутов и повторных запусков TUN |
| Сборки Windows | Штатные workflow родительского проекта | Добавлены отдельные ручные сборки Light, Medium и Full для Windows x64 |
| Пользовательская конфигурация | Стандартный состав сборки | Во все три сборки рядом с `v2rayN.exe` добавляется настроенный файл `agent_v` |

### Загрузка сборок

Откройте раздел [GitHub Actions](https://github.com/PrtUsr1976/v2rayN/actions), выберите нужный workflow и нажмите **Run workflow**. После завершения откройте запуск и скачайте созданный артефакт.

| Сборка | Состав | Требования | Workflow |
|---|---|---|---|
| **Light** | Приложение и библиотеки; без встроенного .NET; без прокси-ядер | Требуется отдельно установить подходящий .NET Desktop Runtime | [Build Windows Light](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml) |
| **Medium** | Приложение, библиотеки и встроенный .NET; без прокси-ядер | Отдельная установка .NET не требуется | [Build Windows Medium](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml) |
| **Full** | Приложение, встроенный .NET и прокси-ядра | Полный готовый к работе архив | [Build Windows Full](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml) |

Все три пользовательские сборки запускаются только вручную, предназначены для Windows x64 и содержат `agent_v` рядом с `v2rayN.exe`.

### Настройка `agent_v`

Пример:

```ini
user_agent=Throne/1.1.6
x_hwid=00000000-0000-0000-0000-000000000000
x_device_os=Windows
x_ver_os=10.0.17763
x_device_model=VirtualBox
```

Соответствие параметров заголовкам:

| Параметр | HTTP-заголовок |
|---|---|
| `user_agent` | `User-Agent` |
| `x_hwid` | `x-hwid` |
| `x_device_os` | `x-device-os` |
| `x_ver_os` | `x-ver-os` |
| `x_device_model` | `x-device-model` |

Парсер поддерживает UTF-8 BOM, переводы строк CRLF, пустые строки, комментарии с `;` или `#`, пробелы вокруг ключей и значений, а также повторяющиеся ключи — используется последнее значение.

### Отложенный и устойчивый запуск TUN

Включить TUN после задержки:

```text
v2rayN.exe -tundelay 30
```

Запустить без TUN и оставить его включение пользователю:

```text
v2rayN.exe -tundelay 0
```

Время наблюдения за запуском TUN задаётся в `finetunes.ini`:

```ini
TunStartObservationSeconds=20
```

Допустимый диапазон — от 20 до 300 секунд. Недопустимое значение автоматически заменяется на `20`.

### Ссылки

- [Этот форк](https://github.com/PrtUsr1976/v2rayN)
- [Пользовательские сборки](https://github.com/PrtUsr1976/v2rayN/actions)
- [Родительский проект](https://github.com/2dust/v2rayN)
- [Документация родительского проекта](https://github.com/2dust/v2rayN/wiki)
