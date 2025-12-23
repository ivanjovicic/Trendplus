interface FormFieldProps {
    label: string;
    name: string;
    register: any;
    errors: any;
    type?: string;
    placeholder?: string;
}

export function FormField({ label, name, register, errors, type = "text", placeholder }: FormFieldProps) {
    return (
        <div className="flex flex-col mb-4">
            <label className="font-medium mb-1">{label}</label>
            <input
                type={type}
                placeholder={placeholder}
                {...register(name)}
                className="border rounded-lg px-3 py-2 text-lg shadow-sm focus:ring-2 focus:ring-blue-500"
            />
            {errors[name] && (
                <p className="text-red-600 text-sm mt-1">{errors[name].message}</p>
            )}
        </div>
    );
}
