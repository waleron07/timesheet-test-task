import { useState } from "react";
import { ProjectReportPage } from "./components/ProjectReportPage";
import { TimeEntriesPage } from "./components/TimeEntriesPage";

type Tab = "timesheet" | "report";

export const App = () => {
    const [tab, setTab] = useState<Tab>("timesheet");

    return (
        <div className="app">
            <h1>Учёт трудозатрат</h1>

            <nav className="tabs">
                <button
                    className={tab === "timesheet" ? "tab active" : "tab"}
                    onClick={() => setTab("timesheet")}
                >
                    Табель
                </button>
                <button className={tab === "report" ? "tab active" : "tab"} onClick={() => setTab("report")}>
                    Отчёт по проектам
                </button>
            </nav>

            {tab === "timesheet" ? <TimeEntriesPage /> : <ProjectReportPage />}
        </div>
    );
};
