import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type {
    ClosedPeriodDto,
    EmployeeDto,
    PagedTimeEntriesDto,
    ProjectDto,
    ProjectReportDto,
    TimeEntryPayload
} from "./types";

export interface TimeEntriesFilter {
    year: number;
    month: number;
    employeeId: string;
    projectId: string;
    page: number;
    pageSize: number;
}

function timeEntriesUrl(filter: TimeEntriesFilter): string {
    // URLSearchParams вместо конкатенации: экранирование и пропуск пустых
    // фильтров — не то, что стоит писать руками.
    const query = new URLSearchParams({
        year: String(filter.year),
        month: String(filter.month),
        page: String(filter.page),
        pageSize: String(filter.pageSize)
    });

    // Фильтры уходят на сервер, а не применяются к загруженной странице:
    // клиентская фильтрация несовместима с серверной пагинацией.
    if (filter.employeeId) query.set("employeeId", filter.employeeId);
    if (filter.projectId) query.set("projectId", filter.projectId);

    return `/api/time-entries?${query}`;
}

export function useTimeEntries(filter: TimeEntriesFilter) {
    return useQuery({
        queryKey: ["time-entries", filter],
        queryFn: ({ signal }) => api.get<PagedTimeEntriesDto>(timeEntriesUrl(filter), signal)
    });
}

export function useEmployees() {
    return useQuery({
        queryKey: ["employees"],
        queryFn: ({ signal }) => api.get<EmployeeDto[]>("/api/employees", signal),
        staleTime: Infinity // справочник за сессию не меняется
    });
}

export function useProjects() {
    return useQuery({
        queryKey: ["projects"],
        queryFn: ({ signal }) => api.get<ProjectDto[]>("/api/projects", signal),
        staleTime: Infinity
    });
}

export function useProjectReport(year: number, month: number) {
    return useQuery({
        queryKey: ["report", year, month],
        queryFn: ({ signal }) =>
            api.get<ProjectReportDto>(`/api/reports/projects?year=${year}&month=${month}`, signal)
    });
}

export function useClosedPeriods() {
    return useQuery({
        queryKey: ["closed-periods"],
        queryFn: ({ signal }) => api.get<ClosedPeriodDto[]>("/api/periods", signal)
    });
}

/**
 * После любой мутации инвалидируются и список, и отчёт: стоимость, применённую
 * ставку и признак переработки считает сервер, поэтому собирать новое состояние
 * на клиенте нельзя — оно разъедется с базой. Это и есть причина выбора
 * react-query, описанная в NOTES.md.
 */
function useInvalidateTimesheet() {
    const queryClient = useQueryClient();

    return () => {
        void queryClient.invalidateQueries({ queryKey: ["time-entries"] });
        void queryClient.invalidateQueries({ queryKey: ["report"] });
    };
}

export function useCreateTimeEntry() {
    const invalidate = useInvalidateTimesheet();

    return useMutation({
        mutationFn: (payload: TimeEntryPayload) => api.put<{ id: string }>("/api/time-entries", payload),
        onSuccess: invalidate
    });
}

export function useUpdateTimeEntry() {
    const invalidate = useInvalidateTimesheet();

    return useMutation({
        mutationFn: ({ id, ...payload }: TimeEntryPayload & { id: string; version: number }) =>
            api.post<void>(`/api/time-entries/${encodeURIComponent(id)}`, payload),
        onSuccess: invalidate
    });
}

export function useDeleteTimeEntry() {
    const invalidate = useInvalidateTimesheet();

    return useMutation({
        mutationFn: (id: string) => api.delete<void>(`/api/time-entries/${encodeURIComponent(id)}`),
        onSuccess: invalidate
    });
}

export function useTogglePeriod() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({ year, month, close }: { year: number; month: number; close: boolean }) =>
            api.post<void>(`/api/periods/${close ? "close" : "open"}`, { year, month }),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ["closed-periods"] });
        }
    });
}
