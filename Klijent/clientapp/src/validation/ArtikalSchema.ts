import { z } from "zod";

export const ArtikalSchema = z.object({
    naziv: z.string().min(2, "Naziv mora imati bar 2 slova"),
    prodajnaCena: z.number().min(1, "Prodajna cena je obavezna"),
    nabavnaCena: z.number().min(1, "Nabavna cena je obavezna"),
    nabavnaCenaDin: z.number().min(0),
    prvaProdajnaCena: z.number().min(1),
    kolicina: z.number().min(0),
    komentar: z.string().optional(),
    tipObuceId: z.string().min(1, "Izaberite tip obu?e"),
    dobavljacId: z.string().min(1, "Izaberite dobavlja?a"),
});
