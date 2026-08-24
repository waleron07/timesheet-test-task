import { useState } from "react";
import { errorMessage } from "../api/client";
import { useCreateTimeEntry, useUpdateTimeEntry } from "../api/hooks";
import type { EmployeeDto, ProjectDto, TimeEntryDto } from "../api/types";
import { ErrorBanner, Modal } from "./common";

const MAX_HOURS = 24;
const HOURS_STEP = 0.5;

interface Props {
    entry: TimeEntryDto | null; // null — создание новой записи
    employees: EmployeeDto[];
    projects: ProjectDto[];
    defaultDate: string;
    onClose: () => void;
}

/**
 * Клиентская валидация — только ради скорости обратной связи. Она дублирует
 * серверную и ничего не гарантирует: те же правила проверяются на бэкенде,
 * и именно его ответ считается истиной.
 */
function validate(employeeId: string, projectId: string, date: string, hours: string): string | null {
    if (!employeeId) return "Выберите сотрудника.";
    if (!projectId) return "Выберите проект.";
    if (!date) return "Укажите дату.";

    const parsed = Number(hours.replace(",", "."));
    if (!Number.isFinite(parsed)) return "Часы должны быть числом.";
    if (parsed <= 0) return "Количество часов должно быть больше нуля.";
    if (parsed > MAX_HOURS) return `Количество часов не может превышать ${MAX_HOURS}.`;
    if (!Number.isInteger(parsed * 2)) return "Количество часов должно быть кратно 0,5.";

    return null;
}

export const TimeEntryModal = ({ entry, employees, projects, defaultDate, onClose }: Props) => {
    const isEdit = entry !== null;

    const [employeeId, setEmployeeId] = useState(entry?.employeeId ?? "");
    const [projectId, setProjectId] = useState(entry?.projectId ?? "");
    const [date, setDate] = useState(entry?.date ?? defaultDate);
    const [hours, setHours] = useState(entry ? String(entry.hours) : "");
    const [comment, setComment] = useState(entry?.comment ?? "");
    const [error, setError] = useState<string | null>(null);

    const create = useCreateTimeEntry();
    const update = useUpdateTimeEntry();
    const saving = create.isPending || update.isPending;

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();

        const validationError = validate(employeeId, projectId, date, hours);
        if (validationError) {
            setError(validationError);
            return;
        }

        const payload = {
            employeeId,
            projectId,
            date,
            hours: Number(hours.replace(",", ".")),
            comment: comment.trim() ? comment.trim() : null
        };

        try {
            if (isEdit) {
                // version — та, с которой запись открыли на редактирование.
                // Если её уже изменили, сервер ответит 409, и пользователь
                // увидит внятный отказ вместо тихой перезаписи чужой правки.
                await update.mutateAsync({ ...payload, id: entry.id, version: entry.version });
            } else {
                await create.mutateAsync(payload);
            }
            onClose();
        } catch (e) {
            setError(errorMessage(e));
        }
    };

    return (
        <Modal title={isEdit ? "Изменение записи" : "Новая запись"} onClose={onClose}>
            <form onSubmit={submit}>
                {error && <ErrorBanner message={error} />}

                <label>
                    Сотрудник
                    <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
                        <option value="">— выберите —</option>
                        {employees.map((employee) => (
                            <option key={employee.id} value={employee.id}>
                                {employee.fullName}
                            </option>
                        ))}
                    </select>
                </label>

                <label>
                    Проект
                    <select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
                        <option value="">— выберите —</option>
                        {projects.map((project) => (
                            <option key={project.id} value={project.id}>
                                {project.code} — {project.name}
                            </option>
                        ))}
                    </select>
                </label>

                <label>
                    Дата
                    {/* type="date" даёт ISO YYYY-MM-DD независимо от локали браузера */}
                    <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
                </label>

                <label>
                    Часы
                    <input
                        type="number"
                        step={HOURS_STEP}
                        min={HOURS_STEP}
                        max={MAX_HOURS}
                        value={hours}
                        onChange={(e) => setHours(e.target.value)}
                    />
                </label>

                <label>
                    Комментарий
                    <input
                        type="text"
                        maxLength={500}
                        value={comment}
                        onChange={(e) => setComment(e.target.value)}
                    />
                </label>

                <div className="modal-actions">
                    <button type="button" className="link" onClick={onClose}>
                        Отмена
                    </button>
                    {/* disabled на время запроса: иначе двойной клик создаёт дубль */}
                    <button type="submit" disabled={saving}>
                        {saving ? "Сохранение..." : "Сохранить"}
                    </button>
                </div>
            </form>
        </Modal>
    );
};
