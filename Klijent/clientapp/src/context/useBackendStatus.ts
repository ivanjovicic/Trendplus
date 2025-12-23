import { useContext } from "react";
import { BackendStatusContext } from "./BackendStatusContext";

export const useBackendStatus = () => useContext(BackendStatusContext);
