import { useEffect, useState } from "react";
import type { TipObuce } from "../types/TipObuce";
import type { Dobavljac } from "../types/Dobavljaci";
import { getTipoviObuce } from "../services/tipoviObuceApi";
import { getDobavljaci } from "../services/dobavljaciApi";

export function useDropdownData() {
    const [tipoviObuce, setTipoviObuce] = useState<TipObuce[]>([]);
    const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);

    useEffect(() => {
        getTipoviObuce().then(setTipoviObuce).catch(console.error);
        getDobavljaci().then(setDobavljaci).catch(console.error);
    }, []);

    return { tipoviObuce, dobavljaci };
}
