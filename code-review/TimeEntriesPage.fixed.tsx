// Исправленная версия TimeEntriesPage.tsx (часть 1 задания).
//
// Что исправлено по сравнению с оригиналом (подробности — в REVIEW.md):
//   1. useEffect без зависимостей → бесконечный цикл запросов (п. 1);
//      заодно экран теперь реагирует на смену месяца (п. 2).
//   2. entries.push(...) + setEntries(entries) — мутация состояния, рендера не
//      было (п. 3); в состояние клался объект запроса вместо ответа сервера,
//      из-за чего entry.amount.toFixed падал бы с TypeError (п. 4).
//   3. Ответы сервера не проверялись вообще: все бизнес-ошибки (закрытый
//      период, лимит 24 часа, нет ставки, конфликт версий) терялись, а
//      пользователю показывался alert «Сохранено» (п. 5). Теперь ошибка
//      разбирается и выводится в интерфейсе.
//   4. setLoading(false) не выполнялся при ошибке — вечная «Загрузка...» (п. 6).
//   5. Race condition при смене месяца — AbortController (п. 7).
//   6. Дата уходила как toLocaleDateString() — формат зависел от локали
//      браузера (п. 8); теперь <input type="date"> и ISO YYYY-MM-DD.
//   7. Часы уходили строкой без валидации (п. 9) — парсинг + проверка
//      кратности 0,5 и границ до отправки.
//   8. Типы вместо any[] (п. 12), key={entry.id} вместо индекса (п. 13),
//      итог через useMemo без NaN-заражения (п. 10), === вместо == (п. 11).
//
// Оставлено за рамками: вынос API-клиента и стора в отдельные модули, форма в
// модальном окне, серверная фильтрация и пагинация, версия записи для
// оптимистичной блокировки — всё это сделано в части 2. Здесь чинятся дефекты
// самого файла, структура остаётся прежней, чтобы диff читался.

import React, { useCallback, useEffect, useMemo, useState } from "react";

interface Props {
    year: number;
    month: number;
}

interface TimeEntryDto {
    id: string;
    employeeId: string;
    employeeName: string;
    projectId: string;
    projectName: string;
    date: string; // ISO, YYYY-MM-DD
    hours: number;
    rate: number;
    amount: number;
    version: number;
}

interface EmployeeDto {
    id: string;
    name: string;
}

/** Ошибка бизнес-правила с бэкенда: машиночитаемый код + текст для человека. */
interface ApiError {
    code: string;
    message: string;
}

const MAX_HOURS_PER_ENTRY = 24;
const HOURS_STEP = 0.5;

/**
 * Единая точка разбора ответа. fetch не бросает на 4xx/5xx — только на сетевом
 * сбое, поэтому response.ok надо проверять руками, иначе 400 с осмысленным
 * текстом выглядит как успех (главный дефект оригинала).
 */
async function apiFetch<T>(input: string, init?: RequestInit): Promise<T> {
    const response = await fetch(input, init);

    if (!response.ok) {
        let error: ApiError | null = null;
        try {
            error = (await response.json()) as ApiError;
        } catch {
            // тело не JSON (прокси вернул HTML, пустой 502 и т. п.)
        }
        throw new Error(
            error?.message ?? `Запрос завершился с ошибкой ${response.status}. Попробуйте ещё раз.`
        );
    }

    if (response.status === 204) {
        return undefined as unknown as T;
    }

    return (await response.json()) as T;
}

function validateForm(date: string, projectId: string, employeeId: string, hours: string): string | null {
    if (!employeeId) return "Выберите сотрудника.";
    if (!projectId) return "Укажите проект.";
    if (!date) return "Укажите дату.";

    const parsedHours = Number(hours.replace(",", "."));
    if (!Number.isFinite(parsedHours)) return "Часы должны быть числом.";
    if (parsedHours <= 0) return "Часы должны быть больше нуля.";
    if (parsedHours > MAX_HOURS_PER_ENTRY) return `Часы не могут превышать ${MAX_HOURS_PER_ENTRY} за одну запись.`;
    // Умножаем на 2 и сравниваем с целым: прямой остаток от 0.5 в double
    // тоже сработал бы, но так нагляднее и без сюрпризов с точностью.
    if (!Number.isInteger(parsedHours * 2)) return "Часы должны быть кратны 0,5.";

    return null;
}

export const TimeEntriesPage = (props: Props) => {
    const { year, month } = props;

    const [entries, setEntries] = useState<TimeEntryDto[]>([]);
    const [employees, setEmployees] = useState<EmployeeDto[]>([]);
    const [employeeId, setEmployeeId] = useState("");
    const [hours, setHours] = useState("");
    const [date, setDate] = useState("");
    const [projectId, setProjectId] = useState("");
    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);

    /**
     * signal нужен, чтобы ответ за прошлый месяц не перетёр текущий: при
     * быстрой смене месяца порядок ответов не гарантирован.
     */
    const load = useCallback(
        async (signal?: AbortSignal) => {
            setLoading(true);
            setError(null);
            try {
                const query = new URLSearchParams({ year: String(year), month: String(month) });
                const data = await apiFetch<TimeEntryDto[]>(`/api/time-entries?${query}`, { signal });
                setEntries(data);
            } catch (e) {
                if ((e as Error).name === "AbortError") return; // штатная отмена
                setError((e as Error).message);
            } finally {
                // Именно finally: иначе при ошибке экран навсегда залипал
                // в «Загрузка...».
                if (!signal?.aborted) setLoading(false);
            }
        },
        [year, month]
    );

    // Зависимости указаны явно: эффект срабатывает при монтировании и при
    // смене месяца — и только тогда.
    useEffect(() => {
        const controller = new AbortController();
        void load(controller.signal);
        return () => controller.abort();
    }, [load]);

    useEffect(() => {
        const controller = new AbortController();
        apiFetch<EmployeeDto[]>("/api/employees", { signal: controller.signal })
            .then(setEmployees)
            .catch((e: Error) => {
                if (e.name !== "AbortError") setError(e.message);
            });
        return () => controller.abort();
    }, []);

    // Фильтрация по сотруднику остаётся клиентской только потому, что этот
    // экран пока грузит месяц целиком. С серверной пагинацией её обязательно
    // надо переносить в query-параметры, иначе фильтруется одна страница.
    const filtered = useMemo(
        () => (employeeId ? entries.filter((e) => e.employeeId === employeeId) : entries),
        [entries, employeeId]
    );

    const totals = useMemo(() => {
        const acc = filtered.reduce(
            (sum, entry) => ({
                hours: sum.hours + (Number(entry.hours) || 0),
                amount: sum.amount + (Number(entry.amount) || 0)
            }),
            { hours: 0, amount: 0 }
        );
        // Округление в конце: иначе накопленная погрешность double в JS
        // расходится с копейками, посчитанными бэкендом в decimal.
        return { hours: acc.hours, amount: Math.round(acc.amount * 100) / 100 };
    }, [filtered]);

    const save = async () => {
        setNotice(null);

        const validationError = validateForm(date, projectId, employeeId, hours);
        if (validationError) {
            setError(validationError);
            return;
        }

        setSaving(true);
        setError(null);
        try {
            await apiFetch<TimeEntryDto>("/api/time-entries", {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    employeeId,
                    projectId,
                    date, // ISO YYYY-MM-DD прямо из <input type="date">
                    hours: Number(hours.replace(",", "."))
                })
            });

            // Перезагружаем список вместо ручной склейки состояния: стоимость,
            // применённую ставку и признак переработки считает сервер, и только
            // он знает итоговое состояние дня.
            await load();
            setNotice("Запись сохранена.");
            setHours("");
        } catch (e) {
            // Сюда приходят бизнес-ошибки: закрытый период, лимит 24 часа,
            // отсутствие ставки на дату, выход за границы проекта.
            setError((e as Error).message);
        } finally {
            setSaving(false);
        }
    };

    const remove = async (entry: TimeEntryDto) => {
        if (!window.confirm(`Удалить запись от ${entry.date} (${entry.hours} ч)?`)) return;

        setNotice(null);
        setError(null);
        try {
            await apiFetch<void>(`/api/time-entries/${encodeURIComponent(entry.id)}`, { method: "DELETE" });
            await load();
            setNotice("Запись удалена.");
        } catch (e) {
            setError((e as Error).message);
        }
    };

    return (
        <div style={{ padding: 20 }}>
            <h2>
                Табель за {month}.{year}
            </h2>

            {error && (
                <div role="alert" style={{ margin: "12px 0", padding: 10, border: "1px solid #c00", color: "#c00" }}>
                    {error}
                </div>
            )}
            {notice && <div style={{ margin: "12px 0", padding: 10, border: "1px solid #093" }}>{notice}</div>}

            <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
                <option value="">Все сотрудники</option>
                {employees.map((emp) => (
                    <option key={emp.id} value={emp.id}>
                        {emp.name}
                    </option>
                ))}
            </select>

            <div style={{ marginTop: 20 }}>
                <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
                <input placeholder="Проект" value={projectId} onChange={(e) => setProjectId(e.target.value)} />
                <input
                    type="number"
                    step={HOURS_STEP}
                    min={HOURS_STEP}
                    max={MAX_HOURS_PER_ENTRY}
                    placeholder="Часы"
                    value={hours}
                    onChange={(e) => setHours(e.target.value)}
                />
                {/* disabled на время запроса: иначе двойной клик создаёт дубль */}
                <button onClick={save} disabled={saving}>
                    {saving ? "Сохранение..." : "Добавить"}
                </button>
            </div>

            {loading && <div>Загрузка...</div>}

            <table style={{ marginTop: 20, width: "100%" }}>
                <thead>
                    <tr>
                        <th>Дата</th>
                        <th>Сотрудник</th>
                        <th>Проект</th>
                        <th>Часы</th>
                        <th>Ставка</th>
                        <th>Стоимость</th>
                        <th />
                    </tr>
                </thead>
                <tbody>
                    {!loading && filtered.length === 0 && (
                        <tr>
                            <td colSpan={7}>Записей за выбранный месяц нет.</td>
                        </tr>
                    )}
                    {filtered.map((entry) => (
                        <tr key={entry.id}>
                            <td>{entry.date}</td>
                            <td>{entry.employeeName}</td>
                            <td>{entry.projectName}</td>
                            <td>{entry.hours}</td>
                            <td>{(Number(entry.rate) || 0).toFixed(2)}</td>
                            {/* Number(...) || 0 — защита от того, что поле придёт
                                пустым: в оригинале здесь падал весь экран. */}
                            <td>{(Number(entry.amount) || 0).toFixed(2)}</td>
                            <td>
                                <button onClick={() => remove(entry)}>Удалить</button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>

            <div style={{ marginTop: 10 }}>
                Итого: {totals.hours} ч, {totals.amount.toFixed(2)} руб.
            </div>
        </div>
    );
};
