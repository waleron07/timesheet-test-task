import { useState } from "react";
import { errorMessage } from "../api/client";
import {
    useClosedPeriods,
    useDeleteTimeEntry,
    useEmployees,
    useProjects,
    useTimeEntries,
    useTogglePeriod
} from "../api/hooks";
import type { TimeEntryDto } from "../api/types";
import { ErrorBanner, MonthPicker, Notice, formatDate, hours, money } from "./common";
import { TimeEntryModal } from "./TimeEntryModal";

const PAGE_SIZE = 25;

export const TimeEntriesPage = () => {
    const [year, setYear] = useState(2026);
    const [month, setMonth] = useState(3);
    const [employeeId, setEmployeeId] = useState("");
    const [projectId, setProjectId] = useState("");
    const [page, setPage] = useState(1);

    const [editing, setEditing] = useState<TimeEntryDto | null>(null);
    const [modalOpen, setModalOpen] = useState(false);
    const [actionError, setActionError] = useState<string | null>(null);
    const [notice, setNotice] = useState<string | null>(null);

    const employees = useEmployees();
    const projects = useProjects();
    const closedPeriods = useClosedPeriods();
    const remove = useDeleteTimeEntry();
    const togglePeriod = useTogglePeriod();

    const entries = useTimeEntries({ year, month, employeeId, projectId, page, pageSize: PAGE_SIZE });

    const isClosed = (closedPeriods.data ?? []).some((p) => p.year === year && p.month === month);
    const data = entries.data;
    const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

    const changeMonth = (nextYear: number, nextMonth: number) => {
        setYear(nextYear);
        setMonth(nextMonth);
        setPage(1); // иначе можно остаться на несуществующей странице нового месяца
    };

    const openCreate = () => {
        setEditing(null);
        setModalOpen(true);
    };

    const openEdit = (entry: TimeEntryDto) => {
        setEditing(entry);
        setModalOpen(true);
    };

    const closeModal = () => {
        setModalOpen(false);
        setEditing(null);
    };

    const onDelete = async (entry: TimeEntryDto) => {
        setActionError(null);
        setNotice(null);

        if (!window.confirm(`Удалить запись от ${formatDate(entry.date)} (${hours(entry.hours)} ч)?`)) return;

        try {
            await remove.mutateAsync(entry.id);
            setNotice("Запись удалена.");
        } catch (e) {
            // Сюда попадает и отказ по закрытому периоду.
            setActionError(errorMessage(e));
        }
    };

    const onTogglePeriod = async () => {
        setActionError(null);
        try {
            await togglePeriod.mutateAsync({ year, month, close: !isClosed });
            setNotice(isClosed ? "Период открыт." : "Период закрыт.");
        } catch (e) {
            setActionError(errorMessage(e));
        }
    };

    return (
        <div>
            <div className="toolbar">
                <MonthPicker year={year} month={month} onChange={changeMonth} />

                <select
                    value={employeeId}
                    onChange={(e) => {
                        setEmployeeId(e.target.value);
                        setPage(1);
                    }}
                >
                    <option value="">Все сотрудники</option>
                    {(employees.data ?? []).map((employee) => (
                        <option key={employee.id} value={employee.id}>
                            {employee.fullName}
                        </option>
                    ))}
                </select>

                <select
                    value={projectId}
                    onChange={(e) => {
                        setProjectId(e.target.value);
                        setPage(1);
                    }}
                >
                    <option value="">Все проекты</option>
                    {(projects.data ?? []).map((project) => (
                        <option key={project.id} value={project.id}>
                            {project.code}
                        </option>
                    ))}
                </select>

                <button onClick={openCreate}>Добавить запись</button>

                <button className="secondary" onClick={onTogglePeriod}>
                    {isClosed ? "Открыть период" : "Закрыть период"}
                </button>

                {isClosed && <span className="badge badge-closed">Период закрыт бухгалтерией</span>}
            </div>

            {actionError && <ErrorBanner message={actionError} />}
            {entries.isError && <ErrorBanner message={errorMessage(entries.error)} />}
            {notice && <Notice message={notice} />}

            <table>
                <thead>
                    <tr>
                        <th>Дата</th>
                        <th>Сотрудник</th>
                        <th>Проект</th>
                        <th className="num">Часы</th>
                        <th className="num">Ставка</th>
                        <th className="num">Стоимость</th>
                        <th>Комментарий</th>
                        <th>Переработка</th>
                        <th />
                    </tr>
                </thead>
                <tbody>
                    {entries.isLoading && (
                        <tr>
                            <td colSpan={9}>Загрузка...</td>
                        </tr>
                    )}

                    {!entries.isLoading && data?.items.length === 0 && (
                        <tr>
                            <td colSpan={9}>За выбранный месяц записей нет.</td>
                        </tr>
                    )}

                    {(data?.items ?? []).map((entry) => (
                        // key по id, а не по индексу: при удалении строки из
                        // середины React иначе переиспользует не тот DOM-узел.
                        <tr key={entry.id} className={entry.isOvertime ? "row-overtime" : undefined}>
                            <td>{formatDate(entry.date)}</td>
                            <td>{entry.employeeName}</td>
                            <td>
                                {entry.projectCode} — {entry.projectName}
                            </td>
                            <td className="num">{hours(entry.hours)}</td>
                            <td className="num">{entry.rate === null ? "—" : money(entry.rate)}</td>
                            <td className="num">{money(entry.amount)}</td>
                            <td>{entry.comment ?? ""}</td>
                            <td>
                                {entry.isOvertime && (
                                    <span className="badge badge-overtime" title="Больше 12 часов за день">
                                        {hours(entry.dayHours)} ч за день
                                    </span>
                                )}
                            </td>
                            <td className="actions">
                                <button className="link" onClick={() => openEdit(entry)}>
                                    Изменить
                                </button>
                                <button className="link danger" onClick={() => void onDelete(entry)}>
                                    Удалить
                                </button>
                            </td>
                        </tr>
                    ))}
                </tbody>
                {data && data.items.length > 0 && (
                    <tfoot>
                        <tr>
                            <td colSpan={3}>Итого по фильтру</td>
                            <td className="num">{hours(data.totalHours)}</td>
                            <td />
                            <td className="num">{money(data.totalAmount)}</td>
                            <td colSpan={3} />
                        </tr>
                    </tfoot>
                )}
            </table>

            {data && data.totalCount > data.pageSize && (
                <div className="pager">
                    <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                        Назад
                    </button>
                    <span>
                        Страница {data.page} из {totalPages}, всего записей {data.totalCount}
                    </span>
                    <button disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                        Вперёд
                    </button>
                </div>
            )}

            {modalOpen && (
                <TimeEntryModal
                    entry={editing}
                    employees={employees.data ?? []}
                    projects={projects.data ?? []}
                    defaultDate={`${year}-${String(month).padStart(2, "0")}-01`}
                    onClose={closeModal}
                />
            )}
        </div>
    );
};
