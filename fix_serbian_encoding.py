# -*- coding: utf-8 -*-
import codecs

file_path = r"C:\Users\Ivan\source\repos\Trendplus2\Klijent\clientapp\src\components\UnosRobeForm.tsx"

# Read with UTF-8
with codecs.open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace ASCII with UTF-8 Serbian
replacements = {
    'Pretrazi i izaberi dobavljaca': 'Pretražite i izaberite dobavlja?a',
    'Pretrazi dobavljace': 'Pretražite dobavlja?e',
    'Ili izaberite iz liste svih dobavljaca': 'Ili izaberite iz liste svih dobavlja?a',
    'Broj racuna:': 'Broj ra?una:',
    'Broj racuna': 'Broj ra?una',
    'Izabrani dobavljac': 'Izabrani dobavlja?',
    'Dobavljac:': 'Dobavlja?:',
}

for old, new in replacements.items():
    content = content.replace(old, new)

# Write back with UTF-8 (no BOM)
with codecs.open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("? Fajl ažuriran sa srpskim slovima!")
