import React from "react";
import ReactDOM from "react-dom/client";
import "./tailwind.css";
import App from "./App";
import { BackendStatusProvider } from "./context/BackendStatusContext";
import { PingControlProvider } from "./context/PingControlContext";
import { RequestActivityProvider } from "./context/RequestActivityContext";
import { installBackendReachabilityFetchLayer } from "./utils/backendReachabilityFetchLayer";
import { installApiFailoverFetchLayer } from "./utils/apiFailover";
import "./skeleton.css";

installApiFailoverFetchLayer();
installBackendReachabilityFetchLayer();

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
    <React.StrictMode>
        <PingControlProvider>
            <RequestActivityProvider>
                <BackendStatusProvider>
                    <App />
                </BackendStatusProvider>
            </RequestActivityProvider>
        </PingControlProvider>
    </React.StrictMode>
);
