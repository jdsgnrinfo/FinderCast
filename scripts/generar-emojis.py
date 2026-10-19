# Genera src/FinderCast/Assets/Emoji.tsv, la tabla de emojis y símbolos del lanzador (":nombre").
# Los emojis, con sus nombres y etiquetas en español e inglés, salen de emojibase-data (licencia
# MIT, ver THIRD-PARTY-NOTICES.md); los símbolos que no son emojis se añaden abajo a mano.
# Columnas: carácter, nombre en español, etiquetas en español, nombre en inglés, etiquetas en inglés.
#
# Uso (desde la raíz del repositorio):
#   python scripts/generar-emojis.py
import json, os, urllib.request

VERSION = '15.3.2'
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = os.path.join(root, 'src', 'FinderCast', 'Assets', 'Emoji.tsv')

def download(locale):
    url = f'https://cdn.jsdelivr.net/npm/emojibase-data@{VERSION}/{locale}/compact.json'
    with urllib.request.urlopen(url) as response:
        return json.load(response)

es = {e['hexcode']: e for e in download('es')}
en = {e['hexcode']: e for e in download('en')}

def clean(text):
    return text.replace('\t', ' ').replace('\n', ' ').strip()

rows = []
for code, e in sorted(es.items(), key=lambda kv: kv[1].get('order', 1 << 30)):
    # Sin grupo (indicadores regionales) o grupo 2 (tonos de piel sueltos): no son emojis útiles.
    if 'group' not in e or e['group'] == 2 or 'order' not in e:
        continue
    other = en.get(code, {})
    rows.append((e['unicode'], clean(e['label']), ' '.join(e.get('tags', [])),
                 clean(other.get('label', e['label'])), ' '.join(other.get('tags', []))))

# Símbolos que no son emojis: (carácter, nombre es, etiquetas es, nombre en, etiquetas en).
symbols = [
    ('©', 'copyright', 'derechos autor', 'copyright', 'rights'),
    ('®', 'marca registrada', 'registrado', 'registered', 'trademark'),
    ('™', 'marca comercial', 'tm', 'trademark', 'tm'),
    ('°', 'grado', 'grados temperatura', 'degree', 'temperature'),
    ('±', 'más o menos', 'mas menos', 'plus minus', ''),
    ('×', 'multiplicación', 'por multiplicar', 'multiplication', 'times'),
    ('÷', 'división', 'dividir', 'division', 'divide'),
    ('≠', 'distinto de', 'diferente no igual', 'not equal', 'different'),
    ('≈', 'aproximadamente', 'casi igual', 'approximately', 'almost equal'),
    ('≤', 'menor o igual', 'menor igual', 'less or equal', ''),
    ('≥', 'mayor o igual', 'mayor igual', 'greater or equal', ''),
    ('∞', 'infinito', '', 'infinity', ''),
    ('√', 'raíz cuadrada', 'raiz', 'square root', 'root'),
    ('π', 'pi', '', 'pi', ''),
    ('∑', 'sumatorio', 'suma sigma', 'summation', 'sum sigma'),
    ('∆', 'delta', 'incremento', 'delta', 'increment'),
    ('µ', 'micro', 'mu', 'micro', 'mu'),
    ('‰', 'por mil', '', 'per mille', ''),
    ('½', 'un medio', 'mitad fracción', 'one half', 'fraction'),
    ('¼', 'un cuarto', 'fracción', 'one quarter', 'fraction'),
    ('¾', 'tres cuartos', 'fracción', 'three quarters', 'fraction'),
    ('²', 'al cuadrado', 'superíndice dos', 'squared', 'superscript two'),
    ('³', 'al cubo', 'superíndice tres', 'cubed', 'superscript three'),
    ('€', 'euro', 'moneda', 'euro', 'currency'),
    ('£', 'libra', 'moneda esterlina', 'pound', 'currency sterling'),
    ('¥', 'yen', 'moneda', 'yen', 'currency'),
    ('¢', 'centavo', 'moneda', 'cent', 'currency'),
    ('₿', 'bitcoin', 'moneda', 'bitcoin', 'currency'),
    ('→', 'flecha derecha', '', 'right arrow', ''),
    ('←', 'flecha izquierda', '', 'left arrow', ''),
    ('↑', 'flecha arriba', '', 'up arrow', ''),
    ('↓', 'flecha abajo', '', 'down arrow', ''),
    ('↔', 'flecha izquierda derecha', 'doble', 'left right arrow', 'double'),
    ('⇒', 'flecha doble derecha', 'implica', 'double right arrow', 'implies'),
    ('↵', 'flecha retorno', 'enter intro', 'return arrow', 'enter'),
    ('⌘', 'comando', 'mac', 'command', 'mac'),
    ('⌥', 'opción', 'mac alt', 'option', 'mac alt'),
    ('⇧', 'mayúsculas', 'shift', 'shift', ''),
    ('⌫', 'retroceso', 'borrar', 'backspace', 'delete'),
    ('•', 'viñeta', 'punto lista', 'bullet', 'dot list'),
    ('·', 'punto medio', '', 'middle dot', ''),
    ('…', 'puntos suspensivos', 'elipsis', 'ellipsis', ''),
    ('—', 'raya', 'guion largo', 'em dash', 'dash'),
    ('–', 'semirraya', 'guion medio', 'en dash', 'dash'),
    ('«', 'comillas angulares abrir', 'latinas', 'left guillemet', 'quotes'),
    ('»', 'comillas angulares cerrar', 'latinas', 'right guillemet', 'quotes'),
    ('“', 'comillas abrir', 'inglesas', 'left double quote', 'quotes'),
    ('”', 'comillas cerrar', 'inglesas', 'right double quote', 'quotes'),
    ('¿', 'interrogación de apertura', 'pregunta', 'inverted question mark', ''),
    ('¡', 'exclamación de apertura', '', 'inverted exclamation mark', ''),
    ('§', 'sección', 'párrafo', 'section', ''),
    ('¶', 'calderón', 'párrafo', 'pilcrow', 'paragraph'),
    ('†', 'cruz', 'daga', 'dagger', 'cross'),
    ('✓', 'marca de verificación', 'check visto', 'check mark', 'tick'),
    ('✗', 'aspa', 'cruz error', 'ballot x', 'cross'),
    ('★', 'estrella negra', 'favorito', 'black star', 'favorite'),
    ('☆', 'estrella blanca', 'favorito', 'white star', 'favorite'),
    ('♪', 'nota musical', 'música', 'music note', ''),
    ('☐', 'casilla vacía', 'checkbox', 'ballot box', 'checkbox'),
    ('☑', 'casilla marcada', 'checkbox', 'ballot box with check', 'checkbox'),
    ('№', 'número', 'numero', 'numero sign', 'number'),
    ('℃', 'grados celsius', 'temperatura', 'degree celsius', 'temperature'),
    ('℉', 'grados fahrenheit', 'temperatura', 'degree fahrenheit', 'temperature'),
    ('α', 'alfa', 'griego', 'alpha', 'greek'),
    ('β', 'beta', 'griego', 'beta', 'greek'),
    ('Ω', 'omega', 'griego ohmio', 'omega', 'greek ohm'),
    ('λ', 'lambda', 'griego', 'lambda', 'greek'),
    ('ñ', 'eñe', 'minúscula', 'n tilde', 'lowercase'),
    ('Ñ', 'eñe mayúscula', '', 'capital n tilde', ''),
]
for symbol in symbols:
    rows.append(symbol)

with open(out, 'w', encoding='utf-8', newline='\n') as f:
    for row in rows:
        f.write('\t'.join(row) + '\n')
print(len(rows), os.path.getsize(out))
