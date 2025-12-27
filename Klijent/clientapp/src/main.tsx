import React from "react";
import ReactDOM from "react-dom/client";
import "./tailwind.css";
import App from "./App";
import { BackendStatusProvider } from "./context/BackendStatusContext";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
    <React.StrictMode>
        <BackendStatusProvider>
            <App />
        </BackendStatusProvider>
    </React.StrictMode>
);