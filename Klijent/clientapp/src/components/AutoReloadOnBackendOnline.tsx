import { useContext, useEffect, useRef } from "react";
import { BackendStatusContext } from "../context/BackendStatusContext";

export default function AutoReloadOnBackendOnline() {
    const { online } = useContext(BackendStatusContext);
    const prevOnline = useRef<boolean>(online);

    useEffect(() => {
        const wasOnline = prevOnline.current;
        prevOnline.current = online;

        // reload only on offline -> online transition
        if (!wasOnline && online) {
            window.location.reload();
        }
    }, [online]);

    return null;
}
