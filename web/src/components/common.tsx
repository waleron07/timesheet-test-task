import type { ReactNode } from "react";

const MONTHS = [
    "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
    "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
];

interface MonthPickerProps {
    year: number;
    month: number;
    onChange: (year: number, month: number) => void;
}

export const MonthPicker = ({ year, month, onChange }: MonthPickerProps) => (
    <span className="month-picker">
        <select value={month} onChange={(e) => onChange(year, Number(e.target.value))}>
            {MONTHS.map((name, index) => (
                <option key={name} value={index + 1}>
                    {name}
                </option>
            ))}
        </select>
        <input
            type="number"
            min={2000}
            max={2100}
            value={year}
            onChange={(e) => onChange(Number(e.target.value), month)}
        />
    </span>
);

/** Ошибка от сервера — на экране, а не только в консоли (требование ТЗ). */
export const ErrorBanner = ({ message }: { message: string }) => (
    <div className="banner banner-error" role="alert">
        {message}
    </div>
);

export const Notice = ({ message }: { message: string }) => (
    <div className="banner banner-ok">{message}</div>
);

/** Деньги форматируются одинаково во всех таблицах. */
export function money(value: number): string {
    return value.toLocaleString("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function hours(value: number): string {
    return value.toLocaleString("ru-RU", { maximumFractionDigits: 2 });
}

/** ISO YYYY-MM-DD → 05.03.2026. Формат обмена — всегда ISO, локаль только для показа. */
export function formatDate(iso: string): string {
    const [y, m, d] = iso.split("-");
    return `${d}.${m}.${y}`;
}

interface ModalProps {
    title: string;
    onClose: () => void;
    children: ReactNode;
}

export const Modal = ({ title, onClose, children }: ModalProps) => (
    <div className="modal-backdrop" onClick={onClose}>
        <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
                <h3>{title}</h3>
                <button className="link" onClick={onClose} aria-label="Закрыть">
                    ✕
                </button>
            </div>
            {children}
        </div>
    </div>
);
