import "server-only";
export const maxExportBytes = 256 * 1024 * 1024;
// The caller transfers ownership of its timeout and abort listener to this stream.
export function exportStream(upstream: Response, controller: AbortController, cleanup: () => void): ReadableStream<Uint8Array> {
    const reader = upstream.body!.getReader();
    let bytes = 0;
    let ended = false;
    const finish = () => { if (!ended) {
        ended = true;
        cleanup();
        controller.signal.removeEventListener("abort", abort);
    } };
    let downstream: ReadableStreamDefaultController<Uint8Array>;
    const abort = () => { if (!ended) {
        const reason = controller.signal.reason;
        finish();
        void reader.cancel(reason).catch(() => { });
        downstream.error(reason);
    } };
    return new ReadableStream({
        start(target) { downstream = target; controller.signal.addEventListener("abort", abort, { once: true }); if (controller.signal.aborted)
            abort(); },
        async pull(target) {
            if (ended)
                return;
            try {
                const chunk = await reader.read();
                if (ended)
                    return;
                if (chunk.done) {
                    finish();
                    target.close();
                    return;
                }
                bytes += chunk.value.byteLength;
                if (bytes > maxExportBytes)
                    throw new Error("Account export exceeds the download limit.");
                target.enqueue(chunk.value);
            }
            catch (error) {
                if (ended)
                    return;
                finish();
                controller.abort(error);
                await reader.cancel(error).catch(() => { });
                target.error(error);
            }
        },
        async cancel(reason) { finish(); controller.abort(reason); await reader.cancel(reason); },
    });
}
