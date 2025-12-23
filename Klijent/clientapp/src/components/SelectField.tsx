interface SelectFieldProps {
    label: string;
    name: string;
    register: any;
    errors: any;
    options: { id: number; naziv: string }[];
}

export function SelectField({ label, name, register, errors, options }: SelectFieldProps) {
    return (
        <div className="flex flex-col mb-4">
            <label className="font-medium mb-1">{label}</label>

            <select
                {...register(name)}
                className="border rounded-lg px-3 py-2 text-lg shadow-sm focus:ring-2 focus:ring-blue-500"
            >
                <option value="">-- izaberite --</option>
                {options.map((o) => (
                    <option key={o.id} value={o.id}>{o.naziv}</option>
                ))}
            </select>

            {errors[name] && (
                <p className="text-red-600 text-sm mt-1">{errors[name].message}</p>
            )}
        </div>
    );
}
