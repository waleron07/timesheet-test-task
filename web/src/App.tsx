import { useEffect, useState } from "react";

interface HealthResponse {
    status: string;
    mongo: string;
}

/**
 * Этап 0: экран-заглушка, подтверждающая, что связка
 * фронт → прокси → API → Mongo поднимается целиком.
 * Экраны «Табель» и «Отчёт по проектам» появляются на этапе 5.
 */
export const App = () => {
    const [health, setHealth] = useState<HealthResponse | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const controller = new AbortController();
        fetch("/api/health", { signal: controller.signal })
            .then((r) => r.json() as Promise<HealthResponse>)
            .then(setHealth)
            .catch((e: Error) => {
                if (e.name !== "AbortError") setError(e.message);
            });
        return () => controller.abort();
    }, []);

    return (
        <div style={{ fontFamily: "system-ui, sans-serif", padding: 24 }}>
            <h1>Учёт трудозатрат</h1>
            <p>Каркас поднят. Экраны появятся на этапе 5.</p>
            {error && <p style={{ color: "#c00" }}>API недоступен: {error}</p>}
            {health && (
                <p>
                    API: {health.status}, Mongo: {health.mongo}
                </p>
            )}
        </div>
    );
};
