import React from "react";
import ReactDOM from "react-dom/client";
import "./tailwind.css";
import App from "./App";
import { BackendStatusProvider } from "./context/BackendStatusContext";
import { PingControlProvider } from "./context/PingControlContext";
import "./skeleton.css";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
    <React.StrictMode>
        <PingControlProvider>
            <BackendStatusProvider>
                <App />
            </BackendStatusProvider>
        </PingControlProvider>
    </React.StrictMode>
);
