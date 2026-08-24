#!/usr/bin/env bash
#
# Прогон всех приёмочных проверок из задания по живому API.
#
# Скрипт сносит данные и наполняет базу заново, поэтому его можно запускать
# сколько угодно раз: сценарии с ошибками создают лишние записи, после которых
# отчёт за март перестаёт сходиться с эталоном.
#
#   ./scripts/acceptance.sh            # против localhost:8080
#   API=http://host:8080 ./scripts/acceptance.sh
#
set -uo pipefail

API="${API:-http://localhost:8080}"
PASS=0
FAIL=0

green() { printf '\033[32m%s\033[0m' "$1"; }
red()   { printf '\033[31m%s\033[0m' "$1"; }

# check <описание> <ожидаемое> <фактическое>
check() {
    if [ "$2" = "$3" ]; then
        printf '  %s %s\n' "$(green '✓')" "$1"
        PASS=$((PASS + 1))
    else
        printf '  %s %s\n     ожидалось: %s\n     получено:  %s\n' "$(red '✗')" "$1" "$2" "$3"
        FAIL=$((FAIL + 1))
    fi
}

# api <метод> <путь> [тело] → печатает "<http-код> <тело>"
api() {
    local method="$1" path="$2" body="${3:-}"
    if [ -n "$body" ]; then
        curl -s -w '\n%{http_code}' -X "$method" "$API$path" \
            -H 'Content-Type: application/json' -d "$body"
    else
        curl -s -w '\n%{http_code}' -X "$method" "$API$path"
    fi
}

status() { tail -n1 <<<"$1"; }
payload() { sed '$d' <<<"$1"; }

jq_get() { python3 -c "import json,sys; print(json.load(sys.stdin)$1)" 2>/dev/null || echo "ПАРСИНГ_НЕ_УДАЛСЯ"; }

reseed() {
    docker compose exec -T api dotnet Timesheet.Api.dll seed >/dev/null 2>&1 \
        || { echo "Не удалось выполнить seed. Поднят ли стек: docker compose up -d?"; exit 1; }
}

echo "API: $API"
echo
echo "Наполнение базы приёмочными данными..."
reseed

# ---------------------------------------------------------------- отчёты
echo
echo "Отчёт за март 2026"
R=$(payload "$(api GET '/api/reports/projects?year=2026&month=3')")
check "П-001: 12 часов"          "12"      "$(jq_get "['rows'][0]['hours']" <<<"$R")"
check "П-001: 7 600 ₽"           "7600.0"  "$(jq_get "['rows'][0]['amount']" <<<"$R")"
check "П-001: освоено 38 %"      "38.0"    "$(jq_get "['rows'][0]['percent']" <<<"$R")"
check "П-001: без перерасхода"   "False"   "$(jq_get "['rows'][0]['overspent']" <<<"$R")"
check "П-002: 10 часов"          "10"      "$(jq_get "['rows'][1]['hours']" <<<"$R")"
check "П-002: 7 000 ₽"           "7000.0"  "$(jq_get "['rows'][1]['amount']" <<<"$R")"
check "П-002: освоено 140 %"     "140.0"   "$(jq_get "['rows'][1]['percent']" <<<"$R")"
check "П-002: перерасход"        "True"    "$(jq_get "['rows'][1]['overspent']" <<<"$R")"
check "Итого: 22 часа"           "22"      "$(jq_get "['totalHours']" <<<"$R")"
check "Итого: 14 600 ₽"          "14600.0" "$(jq_get "['totalAmount']" <<<"$R")"

echo
echo "Отчёт за февраль 2026"
R=$(payload "$(api GET '/api/reports/projects?year=2026&month=2')")
check "П-001: 8 часов"           "8"      "$(jq_get "['rows'][0]['hours']" <<<"$R")"
check "П-001: 4 000 ₽"           "4000.0" "$(jq_get "['rows'][0]['amount']" <<<"$R")"
check "П-001: освоено 20 %"      "20.0"   "$(jq_get "['rows'][0]['percent']" <<<"$R")"

# ------------------------------------------------------- сценарии с ошибками
echo
echo "Сценарии с ошибками"

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-petrova","projectId":"prj-001","date":"2026-01-15","hours":8}')
check "1. Петрова 15.01.2026 — нет ставки: 400" "400" "$(status "$RESP")"
check "1. код ошибки" "RATE_NOT_FOUND" "$(jq_get "['code']" <<<"$(payload "$RESP")")"

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-03-06","hours":20}')
check "2. Иванов 06.03, 20 ч — сохраняется: 201" "201" "$(status "$RESP")"
L=$(payload "$(api GET '/api/time-entries?year=2026&month=3&employeeId=emp-ivanov')")
check "2. день помечен как переработка" "True" \
    "$(python3 -c "
import json,sys
items=json.load(sys.stdin)['items']
print([e['isOvertime'] for e in items if e['date']=='2026-03-06'][0])" <<<"$L")"

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-03-06","hours":6}')
check "3. ещё 6 ч — 26 ч за день: 409" "409" "$(status "$RESP")"
check "3. код ошибки" "DAILY_LIMIT_EXCEEDED" "$(jq_get "['code']" <<<"$(payload "$RESP")")"

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-002","date":"2026-02-20","hours":8}')
check "4. П-002 датой 20.02 — до начала проекта: 400" "400" "$(status "$RESP")"
check "4. код ошибки" "DATE_OUT_OF_PROJECT_RANGE" "$(jq_get "['code']" <<<"$(payload "$RESP")")"

api POST '/api/periods/close' '{"year":2026,"month":2}' >/dev/null
RESP=$(api POST '/api/time-entries/te-001' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-02-20","hours":4,"version":1}')
check "5. правка в закрытом феврале: 409" "409" "$(status "$RESP")"
check "5. код ошибки" "PERIOD_CLOSED" "$(jq_get "['code']" <<<"$(payload "$RESP")")"
api POST '/api/periods/open' '{"year":2026,"month":2}' >/dev/null

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-03-10","hours":0}')
check "6a. часы 0 — валидация: 400" "400" "$(status "$RESP")"
RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-03-10","hours":3.7}')
check "6b. часы 3,7 — валидация: 400" "400" "$(status "$RESP")"

RESP=$(api POST '/api/time-entries/te-003' \
    '{"employeeId":"emp-petrova","projectId":"prj-001","date":"2026-03-05","hours":5,"version":1}')
check "7. первая вкладка сохраняет: 204" "204" "$(status "$RESP")"
RESP=$(api POST '/api/time-entries/te-003' \
    '{"employeeId":"emp-petrova","projectId":"prj-001","date":"2026-03-05","hours":6,"version":1}')
check "7. вторая вкладка получает отказ: 409" "409" "$(status "$RESP")"
check "7. код ошибки" "CONCURRENT_MODIFICATION" "$(jq_get "['code']" <<<"$(payload "$RESP")")"

echo
echo "8. Правка ставки задним числом"
reseed
docker compose exec -T mongo mongosh timesheet --quiet --eval '
db.employees.updateOne({_id:"emp-ivanov","rates.from":ISODate("2026-03-01")},
                       {$set:{"rates.$.value":NumberDecimal("650")}})' >/dev/null
L=$(payload "$(api GET '/api/time-entries?year=2026&month=3&employeeId=emp-ivanov')")
check "8. запись 05.03 стала 5 200 ₽" "5200.0" "$(jq_get "['items'][0]['amount']" <<<"$L")"
R=$(payload "$(api GET '/api/reports/projects?year=2026&month=3')")
check "8. П-001 за март стал 8 000 ₽" "8000.0" "$(jq_get "['rows'][0]['amount']" <<<"$R")"

echo
echo "Дополнительно: границы правил, которые ТЗ не проверяет явно"
reseed
api POST '/api/periods/close' '{"year":2026,"month":2}' >/dev/null

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-02-25","hours":8}')
check "создание записи в закрытом периоде: 409" "409" "$(status "$RESP")"

RESP=$(api DELETE '/api/time-entries/te-001')
check "удаление записи из закрытого периода: 409" "409" "$(status "$RESP")"

# Допущение 1.4 из NOTES.md: закрытый период неизменяем и «на вход», и «на выход».
RESP=$(api POST '/api/time-entries/te-002' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-02-10","hours":8,"version":1}')
check "перенос записи в закрытый период: 409" "409" "$(status "$RESP")"

api POST '/api/periods/open' '{"year":2026,"month":2}' >/dev/null
api POST '/api/periods/close' '{"year":2026,"month":3}' >/dev/null
RESP=$(api POST '/api/time-entries/te-002' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-02-10","hours":8,"version":1}')
check "перенос записи из закрытого периода: 409" "409" "$(status "$RESP")"
api POST '/api/periods/open' '{"year":2026,"month":3}' >/dev/null

# Границы включительные: 24 часа и последний день проекта допустимы.
RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-ivanov","projectId":"prj-001","date":"2026-03-20","hours":24}')
check "ровно 24 часа одной записью: 201" "201" "$(status "$RESP")"

RESP=$(api PUT '/api/time-entries' \
    '{"employeeId":"emp-petrova","projectId":"prj-001","date":"2026-03-31","hours":4}')
check "запись в последний день проекта: 201" "201" "$(status "$RESP")"

echo
echo "Возврат базы в эталонное состояние..."
reseed

echo
if [ "$FAIL" -eq 0 ]; then
    printf '%s\n' "$(green "Все проверки пройдены: $PASS")"
    exit 0
else
    printf '%s\n' "$(red "Пройдено: $PASS, провалено: $FAIL")"
    exit 1
fi
