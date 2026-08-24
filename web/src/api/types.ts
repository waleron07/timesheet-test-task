// Контракты API. Повторяют DTO бэкенда — именно они, а не документы MongoDB:
// переименование поля в базе не должно ломать фронт.

export interface TimeEntryDto {
    id: string;
    employeeId: string;
    employeeName: string;
    projectId: string;
    projectCode: string;
    projectName: string;
    /** ISO, YYYY-MM-DD */
    date: string;
    hours: number;
    /** null, если на дату записи у сотрудника не нашлось ставки. */
    rate: number | null;
    amount: number;
    comment: string | null;
    /** Переработка — признак дня целиком, а не отдельной записи. */
    isOvertime: boolean;
    dayHours: number;
    /** Версия для оптимистичной блокировки: уходит обратно при сохранении. */
    version: number;
}

export interface PagedTimeEntriesDto {
    items: TimeEntryDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    /** Итоги по всей выборке под фильтрами, а не по видимой странице. */
    totalHours: number;
    totalAmount: number;
}

export interface EmployeeDto {
    id: string;
    fullName: string;
    department: string;
}

export interface ProjectDto {
    id: string;
    code: string;
    name: string;
    budget: number;
    startDate: string;
    endDate: string | null;
}

export interface ProjectReportRowDto {
    projectId: string;
    projectCode: string;
    projectName: string;
    hours: number;
    amount: number;
    budget: number;
    /** null, если бюджет не задан: процент от нулевой базы не определён. */
    percent: number | null;
    overspent: boolean;
    atRisk: boolean;
    entriesWithoutRate: number;
}

export interface ProjectReportDto {
    rows: ProjectReportRowDto[];
    totalHours: number;
    totalAmount: number;
}

export interface ClosedPeriodDto {
    year: number;
    month: number;
}

export interface TimeEntryPayload {
    employeeId: string;
    projectId: string;
    date: string;
    hours: number;
    comment: string | null;
}
