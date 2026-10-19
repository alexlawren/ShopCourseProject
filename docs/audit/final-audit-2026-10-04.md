**Финальный технический аудит и UI/UX-полировка ShopCourseProject — 04.10.2026.**

**Вердикт: NOT READY.** Безопасные исправления интерфейса выполнены, сборка чистая, все 454 теста проходят, Docker запускается. Заморозку блокируют три HIGH-находки в существующей backend-логике. Также отсутствует обязательное подтверждение работы приложения в браузере. Backend-находки основаны на коде и допустимых последовательностях конкурентного выполнения; воспроизведение с отказами и параллельными запросами в runtime не выполнялось.

1. **Initial Git state.** Ветка `coursework/dev`, HEAD и `origin/coursework/dev` — `19f7c99`. Рабочая копия изначально содержала изменения. Modified: `docker-compose.yml`, `src/Shop.Gateway/Program.cs`, `src/Shop.Web/Models/Auth/AuthModels.cs`, `src/Shop.Web/Pages/Cart.razor`, `tests/Shop.Web.Tests/ProblemDetailsModelTests.cs`. Untracked: `photo/`, `src/Shop.Web/Services/CheckoutUiHelper.cs`, `tests/Shop.IntegrationTests/GatewayStaticFileConfigurationTests.cs`, `tests/Shop.Web.Tests/CheckoutUiHelperTests.cs`. Начальная статистика tracked diff: 5 файлов, 103 добавления, 82 удаления. Эти изменения сохранены; первоначальные исправления Compose, MIME и OUT_OF_STOCK не отменялись. Запретные Git-команды не использовались.

2. **Technical audit summary.** Проверены solution и зависимости проектов, NuGet, точки запуска всех сервисов, контроллеры, Identity/JWT/refresh, RBAC и ownership, каталог, корзина, checkout, резервации, отмена, gRPC-клиент/сервер, SignalR, YARP, migrations, Compose/Dockerfiles, secrets, frontend API/error helpers и тесты. Межсервисных production ProjectReference нет. Внешние API остаются REST/JSON; Order → Catalog использует gRPC/HTTP2; realtime — SignalR. Shop.Web обращается к Gateway. Каждый DbContext использует свою именованную БД. Gateway остаётся тонким proxy и хостом статического WASM. Архитектура, contracts, enum values, DB schema и расчёты денег не менялись.

   JWT проверяет подпись, issuer, audience и срок действия; роли применяются сервером. Admin endpoints защищены ролью Admin. Customer order/cart endpoints извлекают userId из JWT и проверяют владельца; чужой заказ возвращает 404. OrderHub формирует пользовательскую группу по проверенному subject и admin-группу по роли; CatalogHub распространяет публичные события каталога. Это подтверждение по коду, а не новое end-to-end проникновение. Refresh token криптографически случайный, в БД хранится hash; атомарность ротации недостаточна — H03.

   В tracked конфигурации не обнаружены рабочие JWT keys и DB passwords; локальный `.env` игнорируется Git и не публиковался в отчёте. Проверка `dotnet list ShopCourseProject.sln package --vulnerable --include-transitive` завершилась успешно: известные уязвимые пакеты не найдены по доступному NuGet advisory feed. Это не доказательство отсутствия всех уязвимостей. Production TODO/FIXME/HACK/NotImplementedException не обнаружены; NotImplementedException встречается в неиспользуемых ветках тестовых doubles.

3. **Найденные технические проблемы.** H01–H03 и M01–M02 ниже подтверждены также независимым read-only review. Серьёзные backend-изменения автоматически не применялись согласно ограничениям задачи.

   - H01: `CheckoutService.cs:58,133,214,247` проверяет только `Unavailable` либо полностью игнорирует результат commit. `NotFound`, `AlreadyReleased` и `DownstreamError` могут закончиться ответом Existing/Created. Успех checkout не подтверждает удержание stock.
   - H02: `StockReservationService.cs:35` возвращает существующую Released reservation как успешный reserve. Последовательность: reserve → локальная ошибка сохранения заказа → release → повтор того же RequestId и того же состава/количеств → создание заказа → commit возвращает AlreadyReleased, который игнорируется H01. Stock восстановлен, но заказ принят. Несовпадение состава отдельно проверяет ReserveResponseValidator; незаметная подмена состава через штатный Order flow не заявляется. Также компенсация зависит от успешного внешнего вызова и переданного cancellation token; гарантированного восстановления после прерывания нет.
   - H03: `AuthController.cs:126–161` проверяет IsActive, затем без CAS/row lock/concurrency token отзывает old token и вставляет successor. Два запроса могут оба пройти проверку и выдать два действующих successor tokens. Frontend semaphore не обеспечивает серверную single-use гарантию.
   - M01: `CheckoutService.cs:107–119` сверяет корзину до транзакции на строке 187 и DELETE на строке 191. Обновление Quantity после проверки удаляется вместе со строкой, заказ использует прежний snapshot. Гарантированные duplicate orders с разными RequestId не доказаны и не заявляются: второй DELETE может вызвать EF concurrency exception.
   - M02: `OrderManagementService.cs:126,191` повторно получает уже tracked order без reload между фазами A и C. После финализации конкурирующей отмены stale Status может повторно добавить Cancelled history и notifications. Повторное увеличение stock не заявляется: возврат в Catalog идемпотентен.

4. **Что исправлено.** Устранён recursive refresh при logout с expired token, способный заблокировать frontend semaphore; первый 401 response освобождается перед GET retry. Ошибки read API больше не маскируются пустым каталогом/списком или ложным 404. Raw JSON/HTML/internal error details не попадают в пользовательский alert. Регистрация согласована с password policy backend. Исправлен retry checkout после потерянного ответа: сохранённый непустой RequestId допускает повтор даже при уже очищенной сервером корзине; новый запрос с пустой корзиной запрещён. Флаг выполнения удерживается до завершения обновления корзины. Повторные submit и конфликтующие UI mutations блокируются. Добавлены обработка load failure, retry, корректное обновление details при смене route parameter и fallback для broken images.

5. **UI/UX audit summary.** Структурно проверены Home, Login, Register, Catalog, Product details, Cart, Orders, Order details, Admin panel, Admin Catalog, Admin Orders и Admin Order details; все шесть admin dialogs (create/edit category, create/edit product, stock, image). Сохранены существующие Bootstrap cards/tables, тёмный сине-фиолетовый sidebar и светлый content. Реальная визуальная проверка не выполнена — см. пункты 24–25.

6. **Currency changes.** Единый `MoneyDisplay.Format(decimal)` использует ru-BY и два знака после запятой, например `1199,99 BYN`. Заменены цены и итоги в Catalog, ProductDetails, Cart, Orders, OrderDetails, AdminCatalog, AdminOrders, AdminOrderDetails; подписи price inputs/filters также используют BYN. Типы decimal, значения, snapshot prices и backend calculations сохранены. Финальный поиск в исходниках Shop.Web, исключая build artifacts и vendor libraries: прежний российский символ, прежнее сокращение рублей и российский валютный код отсутствуют. Поиск российского символа по исходному repository также не нашёл совпадений.

7. **Localization fixes.** Duplicate slug показывает «Категория с таким Slug уже существует». Локализованы известные Identity messages и validation errors, 400/401/403/404/409/413/415/429/5xx, loader/application error, admin action captions и role labels. Неизвестные downstream messages заменяются понятным русским fallback. Технические API/Gateway/realtime/Slug/URL/ID и названия продуктов сохранены там, где они полезны для демонстрации.

8. **Admin Orders improvements.** В таблице используются короткие ID вида `4f155e96…`, полный GUID доступен в title; details сохраняет полные OrderId/UserId. Навигация по исходному ID сохранена. Убраны слишком мелкие monospace ID, увеличены row spacing и шрифт, разделены даты, суммы, badges и actions. Filters имеют labels и переносятся. Таблица имеет min-width 54rem и горизонтальную прокрутку внутри wrapper.

9. **Responsive changes.** Для main и flex/grid children добавлен min-width:0; длинные headings/names/ID/messages переносятся. Tables прокручиваются локально вместо расширения страницы. Actions и filters используют flex-wrap. На небольшом экране поля форм складываются, dialogs ограничены viewport и имеют внутреннюю вертикальную прокрутку. Ниже 576px уменьшены избыточные paddings и крупные заголовки. Ниже приведена проверка правил CSS; это не измерение DOM в браузере.

   | Width | Sidebar | Catalog grid | Широкие таблицы / dialogs |
   |---|---|---|---|
   | 1920 | слева 250px | 4 колонки | локальный overflow при необходимости; dialog <= viewport |
   | 1440 | слева 250px | 4 колонки | то же |
   | 1280 | слева 250px | 3 колонки | то же |
   | 1024 | слева 250px | 2 колонки | main min-width:0, локальная прокрутка |
   | 768 | верхняя раскрываемая панель | 2 колонки | горизонтальная прокрутка внутри wrapper |
   | 480 | верхняя раскрываемая панель | 1 колонка | поля stack, dialog margin .5rem |

10. **Sidebar/mobile behavior.** С 1024px остаётся sidebar 250px. До 1024px навигация становится верхней панелью с hamburger, закрытой по умолчанию. aria-expanded/aria-controls связаны с меню. После выбора пункта меню закрывается. Открытое меню занимает не более 65vh и прокручивается, не накладываясь на content. Сторонние библиотеки не добавлялись.

11. **Table improvements.** Admin Catalog, Admin Orders, customer Orders, items и history имеют responsive wrappers, min-width для читаемых колонок, нормальные paddings и font sizes. Wrapper доступен с клавиатуры через tabindex и имеет описание области прокрутки; headers — scope=col. Суммы используют tabular numbers; actions не сжимаются до наложения.

12. **Modal/form improvements.** Ограничена ширина dialogs: min(500px/800px, viewport minus margins). Используется modal-dialog-scrollable. Добавлены связи label/id, role=dialog, aria-modal и aria-labelledby. Ввод и close/save buttons блокируются при saving. Escape закрывает доступный dialog; фокус переводится в dialog, небольшой modal-focus.js удерживает Tab/Shift+Tab внутри активного dialog. Login/Register используют email input и autocomplete. Нативные confirm для удаления/очистки сохранены; их внешний вид зависит от браузера. Полная проверка assistive technology не выполнялась.

13. **Typography changes.** Единый системный font stack сохранён. Main page titles используют h1 с согласованным визуальным размером; body line-height 1.5, table cells .95rem, table headers .9rem, badges .8rem. Labels имеют font-weight 600; captions и длинные заголовки переносятся. Новый web-font не вводился.

14. **Status badge improvements.** Сохранены display mappings: Создан, Подтверждён, В обработке, Отправлен, Выполнен, Отменён; Ожидает оплаты, Оплачен, Отменён. Удалены английские enum suffixes из admin filters/actions. Цветовая семантика Bootstrap сохранена. Backend enum values не изменялись. Disabled buttons имеют заметно меньшую opacity и не выглядят активными.

15. **Error UX changes.** OUT_OF_STOCK, PRODUCT_INACTIVE, CATEGORY_INACTIVE, EMPTY_CART, CART_CHANGED и CATALOG_UNAVAILABLE используют русские code mappings. Price/stock validation и duplicate slug локализованы. 401/403/404/409 имеют безопасные messages. 403/404 страницы оформлены в общем стиле с «На главную». Load failure отличим от пустого результата; alerts сохраняют checkout error при обновлении корзины. Backend raw details, stack traces, HTML и JSON не отображаются.

16. **Accessibility basics.** Добавлены связные label/id, alt/fallback labels изображения, explicit button types, labels для icon/close buttons, focus-visible outline, skip-link, semantic main/h1, table headers и keyboard-scroll wrappers. Dialog получает focus и Tab trap. Это базовые изменения, а не WCAG certification; contrast и keyboard flow в живом браузере требуют smoke.

17. **Files changed.** Полный фактический список изменённых и новых файлов приведён в конце отчёта вместе с Git status. Основные группы: layout/navigation; восемь commerce pages и auth/home/admin home; component ProductImage и scoped CSS; app CSS/index/modal script; API/error/money/checkout helpers и auth handler; frontend regression tests. `docker-compose.yml`, Gateway MIME mappings и integration MIME tests относятся к первоначальной грязной копии и сохранены. Backend service implementation, migrations, contracts, NuGet dependencies и DB schema в ходе этого этапа не изменялись. Исходный `photo/` не изменялся. Временные скрипты находятся в игнорируемом `out/`.

18. **Tests added/changed.** Добавлено 20 test cases: FinalAuditRegressionTests — 7 (legacy text, details suppression, validation/password policy, expired logout без refresh); ApiErrorUxTests — 4 (503 не empty/404, настоящий 404, raw body suppression); MoneyDisplayTests — 3 culture cases; CheckoutUiHelperTests — 6 дополнительных cases (network/502/503 retry с пустой корзиной и pending ID, запрет нового empty checkout). Обновлены ожидаемые русские fallback/action captions в ProblemDetailsModelTests и AdminOrderUiHelperTests. До исправлений новые функциональные regression tests показывали соответствующие failures; после исправлений полный suite зелёный. bUnit и тесты косметического CSS не добавлялись. Начальные MIME/OUT_OF_STOCK regression tests сохранены.

19. **Restore result.** `dotnet restore ShopCourseProject.sln` — exit 0, все проекты восстановлены/актуальны. Песочница мешала первому запуску CLI; успешная проверка выполнена с разрешённым расширением доступа.

20. **Build warnings/errors.** `dotnet build ShopCourseProject.sln` — exit 0, **Warnings=0, Errors=0**. Release publish внутри Docker также завершился успешно. Сообщение publish о необязательном wasm-tools не является compiler warning; workload не устанавливался. Runtime DataProtection/port-binding warnings в Docker выделены отдельно, см. L02.

21. **Test totals.** `dotnet test ShopCourseProject.sln` — exit 0, **454 passed, 0 failed, 0 skipped**. Web 182; Catalog 155; Order 97; Identity 11; Integration 9. Ранее известные 434 увеличились на 20 cases. Наличие зелёного suite не исключает H01–H03 и M01–M02: существующие tests не воспроизводят нужные PostgreSQL interleavings.

22. **Docker Compose config.** `docker compose config --quiet` — exit 0. Quiet применён, чтобы не выводить раскрытые credentials из environment. YAML корректен, initial environment regression не вернулась.

23. **Docker Compose ps.** Выполнен `docker compose up -d --build`, затем после последних изменений `docker compose up -d --build gateway` (Compose также обработал dependencies). Exit 0. Все четыре приложения Up; PostgreSQL Up/healthy. Gateway публикует 8080, остальные сервисы не публикуют host ports; Catalog HTTP2 endpoint 8081 доступен внутри сети. PostgreSQL и product-images volumes сохранены; down -v не выполнялся. Логи подтверждают Application started всех сервисов, startup migrations ранее завершились без ошибок. Compose ps доказывает состояние процессов, а не работоспособность всех пользовательских сценариев.

24. **Browser desktop smoke.** Не выполнен. Запрошенное открытие http://localhost:8080 через browser tool получило отказ доступа: пользователь отклонил разрешение для данного origin. Отказ не обходился другими браузерами, CDP или shell automation. Поэтому загрузка WASM и страницы Home/Catalog/Cart/Orders/Admin Catalog/Admin Orders при 1366/1440px после финального rebuild не подтверждены.

25. **Browser narrow/mobile smoke.** Не выполнен по той же причине. Таблицы, hamburger, filters, dialogs и product cards проверены структурно по Razor/CSS для 1920/1440/1280/1024/768/480px. Фактический page overflow, touch/keyboard flow и видимость buttons при 768/480px не измерялись. Визуальная целостность Customer/Admin оценена по общим tokens/classes/markup, а не по screenshots.

26. **Known remaining issues.** Открыты H01–H03, M01–M05, L01–L02. Общий PostgreSQL login имеет права на все три БД: логическое разделение в коде есть, least-privilege разделения credentials нет. Realtime automatic reconnect после исчерпания попыток блокируется `_isStarted`; state badges не получают отдельного события state change. Docker DataProtection keys находятся в непостоянном нешифрованном хранилище; JWT key из environment от этого не меняется. Эти ограничения не скрываются за UI-полировкой.

27. **Findings table.** Счётчики ниже относятся только к открытым находкам. Исправленные строки приведены отдельно и в открытые counts не включаются.

   | Severity | Component | Finding | Status |
   |---|---|---|---|
   | HIGH | CheckoutService | H01: success при NotFound/AlreadyReleased/DownstreamError commit | OPEN; бизнес-логика не менялась |
   | HIGH | StockReservationService | H02: Released reservation повторно возвращается как успешный reserve; компенсация не гарантирована | OPEN; бизнес-логика не менялась |
   | HIGH | Identity refresh | H03: параллельная ротация выдаёт два successor tokens | OPEN; security fix требуется отдельно |
   | MEDIUM | Checkout/cart | M01: изменение quantity между проверкой и DELETE теряется | OPEN |
   | MEDIUM | Cancellation | M02: stale tracked order позволяет повторные history/notifications | OPEN |
   | MEDIUM | Docker/PostgreSQL | M03: общий привилегированный DB login, нет credential isolation | OPEN; локальное deployment limitation |
   | MEDIUM | Tests | M04: нет PostgreSQL concurrency/fault regression coverage для H01–H03/M01–M02 | OPEN |
   | MEDIUM | Browser verification | M05: desktop/mobile smoke и фактическая загрузка WASM не подтверждены | OPEN; browser access denied |
   | LOW | Realtime frontend | L01: после исчерпания reconnect attempts повторный Start блокируется; badges могут устареть | OPEN |
   | LOW | Docker DataProtection | L02: ephemeral unencrypted key storage/runtime warnings | OPEN; текущий JWT flow не блокирует |
   | MEDIUM | AuthHeaderHandler | Refresh/logout recursion и возможный semaphore deadlock | FIXED + regression test |
   | MEDIUM | Frontend API reads | Outage отображался как empty data/ложный not found | FIXED + regression tests |
   | MEDIUM | Frontend errors | Raw body/details и английские server messages в alerts | FIXED + regression tests |
   | MEDIUM | Cart retry | Retry после durable order/очистки корзины блокировался CanCheckout | FIXED + 6 regression cases |
   | LOW | Register | Frontend принимал пароль, не соответствующий backend policy | FIXED + regression test |
   | COSMETIC | Currency | Неединое обозначение денежных сумм | FIXED: единый BYN + culture tests |
   | COSMETIC | Admin Orders / tables | Плотные IDs/колонки/filters и размеры текста | FIXED по markup/CSS; visual smoke pending |
   | COSMETIC | Mobile / dialogs / images | Desktop sidebar, размеры dialogs, broken image fallback | FIXED по markup/CSS; visual smoke pending |

28. **BLOCKER count:** 0 открытых.

29. **HIGH count:** 3 открытых.

30. **MEDIUM count:** 5 открытых.

31. **LOW count:** 2 открытых.

32. **COSMETIC count:** 0 известных открытых; отсутствие визуальных дефектов не подтверждено без browser smoke.

33. **Final verdict: NOT READY.** Архитектурные границы сохранены, безопасные frontend bugs исправлены, build/test/Compose проходят. Однако stock/order consistency и single-use refresh имеют HIGH-находки, а обязательный browser критерий не выполнен. READY TO FREEZE выдавать нельзя.

34. **Exact minimal remaining work до начала пояснительной записки.**

   1. Локально исправить обработку всех commit statuses и reuse Released reservation как единую задачу согласованности checkout/stock; определить безопасное поведение retry/compensation без смены архитектуры.
   2. Сделать server-side refresh rotation атомарной: условное обновление/CAS либо row lock с проверкой внутри транзакции. Проверить, что два запроса не дают двух active successors.
   3. Закрыть окно cart check/delete и обновлять order entity под lock при финализации cancellation. Не держать DB lock во время долгого gRPC без обоснования.
   4. Добавить regression tests с реальным PostgreSQL/детерминированной синхронизацией для этих interleavings и failure paths. Повторить restore/build/test и Compose rebuild. Новые сервисы или broker для этого не нужны.
   5. Выполнить разрешённый browser smoke после rebuild: Home/Catalog/Cart/Orders/Admin Catalog/Admin Orders, особенно /admin/orders на 1440, 768 и 480; дополнительно пройти forms/dialogs/details, проверить WASM assets, BYN, scroll, actions, errors, pay/cancel. Структурные точки 1920/1280/1024 также подтвердить изменением viewport.
   6. Для локальной курсовой явно документировать M03 и L02 как deployment limitations; для строгого credential isolation дать сервисам отдельные DB роли. L01 проверить длительным отключением сети и устранить перед финальной демонстрацией realtime. Эти пункты не требуют Redis, RabbitMQ, нового UI framework или смены schema.

35. **git diff --stat.** Точный вывод приложен ниже. Он включает первоначальные изменения и показывает только tracked файлы; новые helpers/components/tests/report туда не входят.

36. **git status.** Точный вывод приложен ниже. Ветка и HEAD сохранены. Изменения не staged, commit/push не выполнялись. Работа остановлена после отчёта.

Браузерный smoke остался невыполненным из-за отказа доступа к http://localhost:8080 в browser tool. Альтернативный доступ для обхода отказа не использовался.


**Tracked diff:**

```
 docker-compose.yml                               |  54 +--
 src/Shop.Gateway/Program.cs                      |   2 +
 src/Shop.Web/App.razor                           |   5 +-
 src/Shop.Web/Auth/AuthHeaderHandler.cs           |   4 +-
 src/Shop.Web/Layout/MainLayout.razor             |   5 +-
 src/Shop.Web/Layout/MainLayout.razor.css         |  54 +--
 src/Shop.Web/Layout/NavMenu.razor                |  22 +-
 src/Shop.Web/Layout/NavMenu.razor.css            | 112 ++----
 src/Shop.Web/Models/Auth/AuthModels.cs           |  59 +++-
 src/Shop.Web/Pages/Admin/AdminCatalog.razor      | 428 +++++++++++++----------
 src/Shop.Web/Pages/Admin/AdminHome.razor         |   8 +-
 src/Shop.Web/Pages/Admin/AdminOrderDetails.razor |  52 +--
 src/Shop.Web/Pages/Admin/AdminOrders.razor       |  92 ++---
 src/Shop.Web/Pages/Cart.razor                    | 225 +++++++-----
 src/Shop.Web/Pages/Catalog.razor                 |  80 +++--
 src/Shop.Web/Pages/Home.razor                    |   2 +-
 src/Shop.Web/Pages/Login.razor                   |  15 +-
 src/Shop.Web/Pages/OrderDetails.razor            |  49 ++-
 src/Shop.Web/Pages/Orders.razor                  |  40 +--
 src/Shop.Web/Pages/ProductDetails.razor          |  25 +-
 src/Shop.Web/Pages/Register.razor                |  18 +-
 src/Shop.Web/Services/AdminCatalogApiClient.cs   | 151 +++-----
 src/Shop.Web/Services/AdminOrderApiClient.cs     |  92 ++---
 src/Shop.Web/Services/AdminOrderUiHelper.cs      |   8 +-
 src/Shop.Web/Services/AuthService.cs             |  44 +--
 src/Shop.Web/Services/CartApiClient.cs           |  65 +---
 src/Shop.Web/Services/CatalogApiClient.cs        |  42 +--
 src/Shop.Web/Services/OrderApiClient.cs          |  75 +---
 src/Shop.Web/_Imports.razor                      |   2 +-
 src/Shop.Web/wwwroot/css/app.css                 |  45 ++-
 src/Shop.Web/wwwroot/index.html                  |  11 +-
 tests/Shop.Web.Tests/AdminOrderUiHelperTests.cs  |   8 +-
 tests/Shop.Web.Tests/ProblemDetailsModelTests.cs |  12 +-
 33 files changed, 921 insertions(+), 985 deletions(-)
```

**Final Git status / полный список файлов:**

```
On branch coursework/dev
Your branch is up to date with 'origin/coursework/dev'.

Changes not staged for commit:
  (use "git add <file>..." to update what will be committed)
  (use "git restore <file>..." to discard changes in working directory)
	modified:   docker-compose.yml
	modified:   src/Shop.Gateway/Program.cs
	modified:   src/Shop.Web/App.razor
	modified:   src/Shop.Web/Auth/AuthHeaderHandler.cs
	modified:   src/Shop.Web/Layout/MainLayout.razor
	modified:   src/Shop.Web/Layout/MainLayout.razor.css
	modified:   src/Shop.Web/Layout/NavMenu.razor
	modified:   src/Shop.Web/Layout/NavMenu.razor.css
	modified:   src/Shop.Web/Models/Auth/AuthModels.cs
	modified:   src/Shop.Web/Pages/Admin/AdminCatalog.razor
	modified:   src/Shop.Web/Pages/Admin/AdminHome.razor
	modified:   src/Shop.Web/Pages/Admin/AdminOrderDetails.razor
	modified:   src/Shop.Web/Pages/Admin/AdminOrders.razor
	modified:   src/Shop.Web/Pages/Cart.razor
	modified:   src/Shop.Web/Pages/Catalog.razor
	modified:   src/Shop.Web/Pages/Home.razor
	modified:   src/Shop.Web/Pages/Login.razor
	modified:   src/Shop.Web/Pages/OrderDetails.razor
	modified:   src/Shop.Web/Pages/Orders.razor
	modified:   src/Shop.Web/Pages/ProductDetails.razor
	modified:   src/Shop.Web/Pages/Register.razor
	modified:   src/Shop.Web/Services/AdminCatalogApiClient.cs
	modified:   src/Shop.Web/Services/AdminOrderApiClient.cs
	modified:   src/Shop.Web/Services/AdminOrderUiHelper.cs
	modified:   src/Shop.Web/Services/AuthService.cs
	modified:   src/Shop.Web/Services/CartApiClient.cs
	modified:   src/Shop.Web/Services/CatalogApiClient.cs
	modified:   src/Shop.Web/Services/OrderApiClient.cs
	modified:   src/Shop.Web/_Imports.razor
	modified:   src/Shop.Web/wwwroot/css/app.css
	modified:   src/Shop.Web/wwwroot/index.html
	modified:   tests/Shop.Web.Tests/AdminOrderUiHelperTests.cs
	modified:   tests/Shop.Web.Tests/ProblemDetailsModelTests.cs

Untracked files:
  (use "git add <file>..." to include in what will be committed)
	docs/audit/final-audit-2026-10-04.md
	photo/самсунг25.jpg
	src/Shop.Web/Components/ProductImage.razor
	src/Shop.Web/Components/ProductImage.razor.css
	src/Shop.Web/Pages/Admin/AdminCatalog.razor.css
	src/Shop.Web/Pages/Admin/AdminOrders.razor.css
	src/Shop.Web/Pages/Catalog.razor.css
	src/Shop.Web/Services/ApiErrorReader.cs
	src/Shop.Web/Services/ApiReadHelper.cs
	src/Shop.Web/Services/CheckoutUiHelper.cs
	src/Shop.Web/Services/MoneyDisplay.cs
	src/Shop.Web/wwwroot/js/modal-focus.js
	tests/Shop.IntegrationTests/GatewayStaticFileConfigurationTests.cs
	tests/Shop.Web.Tests/ApiErrorUxTests.cs
	tests/Shop.Web.Tests/CheckoutUiHelperTests.cs
	tests/Shop.Web.Tests/FinalAuditRegressionTests.cs
	tests/Shop.Web.Tests/MoneyDisplayTests.cs

no changes added to commit (use "git add" and/or "git commit -a")
```
