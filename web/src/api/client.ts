// Единая точка обращения к API.
//
// fetch не бросает на 4xx/5xx — только на сетевом сбое, поэтому response.ok
// проверяется здесь один раз для всех запросов. Без этого ошибки бизнес-правил
// (закрытый период, лимит часов, конфликт версий) молча теряются, и
// пользователь видит «сохранено» там, где ничего не сохранилось — ровно тот
// дефект, за который разобран исходный код в REVIEW.md.

/** Ошибка с машиночитаемым кодом от бэкенда. */
export class ApiRequestError extends Error {
    constructor(
        readonly code: string,
        message: string,
        readonly status: number,
        readonly details?: Record<string, string[]>
    ) {
        super(message);
        this.name = "ApiRequestError";
    }

    /** Конфликт: данные изменились или период закрыт — список стоит перечитать. */
    get isConflict(): boolean {
        return this.status === 409;
    }
}

interface ApiErrorBody {
    code?: string;
    message?: string;
    details?: Record<string, string[]>;
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
    const response = await fetch(url, {
        ...init,
        headers: init?.body ? { "Content-Type": "application/json", ...init?.headers } : init?.headers
    });

    if (!response.ok) {
        let body: ApiErrorBody = {};
        try {
            body = (await response.json()) as ApiErrorBody;
        } catch {
            // Тело не JSON: прокси вернул HTML, шлюз — пустой 502 и т. п.
        }

        // Разбор по полям от FluentValidation склеиваем в одну строку:
        // пользователю нужно увидеть, что именно не так, а не код 400.
        const details = body.details;
        const fieldMessages = details ? Object.values(details).flat().join(" ") : "";

        throw new ApiRequestError(
            body.code ?? "UNKNOWN",
            fieldMessages || body.message || `Запрос завершился с ошибкой ${response.status}.`,
            response.status,
            details
        );
    }

    // 204 No Content — у изменения и удаления тела нет.
    if (response.status === 204) return undefined as T;

    return (await response.json()) as T;
}

export const api = {
    get: <T>(url: string, signal?: AbortSignal) => request<T>(url, { signal }),

    put: <T>(url: string, body: unknown) =>
        request<T>(url, { method: "PUT", body: JSON.stringify(body) }),

    post: <T>(url: string, body: unknown) =>
        request<T>(url, { method: "POST", body: JSON.stringify(body) }),

    delete: <T>(url: string) => request<T>(url, { method: "DELETE" })
};

/** Сообщение для пользователя из любой пойманной ошибки. */
export function errorMessage(error: unknown): string {
    if (error instanceof ApiRequestError) return error.message;
    if (error instanceof Error) return `Не удалось связаться с сервером: ${error.message}`;
    return "Неизвестная ошибка.";
}
