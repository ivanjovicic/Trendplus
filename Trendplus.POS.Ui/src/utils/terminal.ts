export default function getTerminalId(): string {
    let id = localStorage.getItem("terminalId");
    if (!id) {
        id = crypto.randomUUID();
        localStorage.setItem("terminalId", id);
    }
    return id;
}
