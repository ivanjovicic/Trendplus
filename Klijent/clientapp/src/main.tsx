import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { BackendStatusProvider } from "./context/BackendStatusContext";
import './tailwind.css';

ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
        <BackendStatusProvider>
            <App />
        </BackendStatusProvider>
    </React.StrictMode>
);