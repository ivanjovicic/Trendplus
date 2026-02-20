export const popularBrands = [
    { label: "Tamaris", value: "tamaris" },
    { label: "Rieker", value: "rieker_1-94255" },
    { label: "Rieker Sport", value: "rieker_sport-975277" },
    { label: "Imac", value: "imac" },
    { label: "Gabor", value: "gabor" },
    { label: "Geox", value: "geox" },
    { label: "Marco Tozzi", value: "marco-tozzi" },
    { label: "S.Oliver", value: "s.oliver" },
    { label: "Vagabond", value: "vagabond" },
    { label: "Tommy Hilfiger", value: "tommy-hilfiger" },
    { label: "Tom Tailor", value: "tom_tailor-547" },
    { label: "Guess", value: "guess" },
    { label: "Buffalo", value: "buffalo" },
    { label: "Nike", value: "nike" },
    { label: "Adidas", value: "adidas" },
    { label: "Puma", value: "puma" },
    { label: "Converse", value: "converse" },
    { label: "Skechers", value: "skechers" },
    { label: "New Balance", value: "new-balance" },
    { label: "Esprit", value: "esprit-461" },
    { label: "Limelight", value: "limelight-481" },
    { label: "Pepe Jeans", value: "pepe_jeans-404067" },
];

// Brands appropriate for Deichmann filters: those that include an id-like suffix (e.g. "-123" or "_123").
export const deichmannBrands = popularBrands.filter(b => /[-_]\d+/.test(b.value));
