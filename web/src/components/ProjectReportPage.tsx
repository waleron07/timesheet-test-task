import { useState } from "react";
import { errorMessage } from "../api/client";
import { useProjectReport } from "../api/hooks";
import { ErrorBanner, MonthPicker, hours, money } from "./common";

export const ProjectReportPage = () => {
    const [year, setYear] = useState(2026);
    const [month, setMonth] = useState(3);

    const report = useProjectReport(year, month);
    const data = report.data;

    const percent = (value: number | null) => (value === null ? "бюджет не задан" : `${money(value)} %`);

    return (
        <div>
            <div className="toolbar">
                <MonthPicker
                    year={year}
                    month={month}
                    onChange={(y, m) => {
                        setYear(y);
                        setMonth(m);
                    }}
                />
            </div>

            {report.isError && <ErrorBanner message={errorMessage(report.error)} />}

            <table>
                <thead>
                    <tr>
                        <th>Проект</th>
                        <th className="num">Часы</th>
                        <th className="num">Стоимость</th>
                        <th className="num">Бюджет</th>
                        <th className="num">Освоено</th>
                        <th>Статус</th>
                    </tr>
                </thead>
                <tbody>
                    {report.isLoading && (
                        <tr>
                            <td colSpan={6}>Загрузка...</td>
                        </tr>
                    )}

                    {!report.isLoading && data?.rows.length === 0 && (
                        <tr>
                            <td colSpan={6}>За выбранный месяц трудозатрат не было.</td>
                        </tr>
                    )}

                    {(data?.rows ?? []).map((row) => (
                        <tr
                            key={row.projectId}
                            className={row.overspent ? "row-overspent" : row.atRisk ? "row-risk" : undefined}
                        >
                            <td>
                                {row.projectCode} — {row.projectName}
                            </td>
                            <td className="num">{hours(row.hours)}</td>
                            <td className="num">{money(row.amount)}</td>
                            <td className="num">{money(row.budget)}</td>
                            <td className="num">{percent(row.percent)}</td>
                            <td>
                                {row.overspent && <span className="badge badge-overspent">перерасход</span>}
                                {!row.overspent && row.atRisk && <span className="badge badge-risk">риск</span>}
                                {/* Записи без ставки дают часы, но не дают денег.
                                    Показываем это явно, а не прячем в итогах. */}
                                {row.entriesWithoutRate > 0 && (
                                    <span className="badge badge-warn" title="У сотрудника нет ставки на дату записи">
                                        без ставки: {row.entriesWithoutRate}
                                    </span>
                                )}
                            </td>
                        </tr>
                    ))}
                </tbody>
                {data && data.rows.length > 0 && (
                    <tfoot>
                        <tr>
                            <td>Итого</td>
                            <td className="num">{hours(data.totalHours)}</td>
                            <td className="num">{money(data.totalAmount)}</td>
                            <td colSpan={3} />
                        </tr>
                    </tfoot>
                )}
            </table>
        </div>
    );
};
