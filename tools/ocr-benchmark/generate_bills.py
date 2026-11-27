#!/usr/bin/env python3
"""
Generates a synthetic benchmark of bill images with known ground truth, for measuring OCR field accuracy.

Each sample is a rendered receipt, invoice, utility bill or pharmacy bill with a bill number, a date and a
total, plus distractors that a naive parser gets wrong (due dates, subtotals, tax lines, item prices).
Images are then degraded the way phone photos and scans are: lower resolution, rotation, blur, noise,
uneven lighting and JPEG compression.

Output (default ./data):
    NNN.png       the bill image
    NNN.pgm       the same image as 8-bit grayscale PGM (read directly by the benchmark runner)
    truth.json    [{id, template, severity, bill_no, date, amount}]   date = yyyy-MM-dd, amount = "1234.50"

Usage:
    python generate_bills.py --count 100 --seed 7 --out data
Requires Pillow (pip install pillow). Uses DejaVu / Liberation / FreeFont TTFs if installed.
"""
import argparse
import datetime as dt
import json
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONT_DIRS = ["/usr/share/fonts", "/usr/local/share/fonts", "C:\\Windows\\Fonts"]
SANS = ["DejaVuSans.ttf", "LiberationSans-Regular.ttf", "FreeSans.ttf", "arial.ttf"]
SANS_BOLD = ["DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf", "FreeSansBold.ttf", "arialbd.ttf"]
SERIF = ["DejaVuSerif.ttf", "LiberationSerif-Regular.ttf", "FreeSerif.ttf", "times.ttf"]
MONO = ["DejaVuSansMono.ttf", "LiberationMono-Regular.ttf", "FreeMono.ttf", "cour.ttf"]

VENDORS = ["Sunrise Medicals", "City Fuel Station", "Metro Stationers", "Green Leaf Pharmacy", "Apex Electronics",
           "Royal Travels", "Blue Star Restaurant", "Prime Office Supplies", "Lakeview Hospital", "Swift Cabs",
           "National Power Distribution Co.", "Urban Water Board", "FastNet Broadband", "Hilltop Diagnostics"]
ITEMS = ["Paracetamol 500mg", "A4 Paper Ream", "Printer Toner", "Consultation Fee", "Blood Test - CBC", "Diesel",
         "Cab Fare", "Lunch Buffet", "USB Cable", "Notebook", "Antibiotic Course", "Courier Charges", "Stapler",
         "Room Rent", "Ink Cartridge", "Train Ticket", "Bandage Roll", "Vitamin D3", "Data Pack", "Parking"]
MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]


_font_paths = None


def pick_font_name(rng, names):
    """A random installed font from `names` (gives the benchmark some typeface variety)."""
    find_font(names, 10)
    installed = [n for n in names if n in _font_paths]
    return [rng.choice(installed)] if installed else names


def find_font(names, size):
    """First font from `names` (in order of preference) that is installed."""
    global _font_paths
    if _font_paths is None:
        _font_paths = {}
        for d in FONT_DIRS:
            for root, _, files in os.walk(d):
                for f in files:
                    _font_paths.setdefault(f, os.path.join(root, f))
    for n in names:
        if n in _font_paths:
            return ImageFont.truetype(_font_paths[n], size)
    return ImageFont.load_default()


def has_glyph(font, ch):
    """True if the font really contains `ch` (not just the empty-box fallback glyph)."""
    try:
        return font.getmask(ch).getbbox() != font.getmask("\uffff").getbbox()
    except Exception:
        return False


def indian_group(n):
    s = f"{n:.2f}"
    whole, frac = s.split(".")
    if len(whole) <= 3:
        return f"{whole}.{frac}"
    head, tail = whole[:-3], whole[-3:]
    parts = []
    while len(head) > 2:
        parts.insert(0, head[-2:])
        head = head[:-2]
    if head:
        parts.insert(0, head)
    return ",".join(parts) + "," + tail + "." + frac


def fmt_amount(rng, value):
    style = rng.choice(["intl", "indian", "plain"])
    body = f"{value:,.2f}" if style == "intl" else indian_group(value) if style == "indian" else f"{value:.2f}"
    prefix = rng.choice(["Rs. ", "Rs.", "\u20b9 ", "\u20b9", "INR ", ""])
    return prefix + body


def fmt_date(rng, d):
    style = rng.randrange(7)
    if style == 0: return d.strftime("%d/%m/%Y")
    if style == 1: return d.strftime("%d-%m-%Y")
    if style == 2: return d.strftime("%d.%m.%Y")
    if style == 3: return d.strftime("%d-%m-%y")
    if style == 4: return f"{d.day:02d} {MONTHS[d.month - 1]} {d.year}"
    if style == 5: return f"{MONTHS[d.month - 1]} {d.day}, {d.year}"
    return f"{d.day:02d}-{MONTHS[d.month - 1]}-{d.year}"


def bill_number(rng):
    style = rng.randrange(6)
    if style == 0: return f"INV-{rng.randint(2023, 2026)}/{rng.randint(1, 9999):04d}"
    if style == 1: return str(rng.randint(1000, 999999))
    if style == 2: return f"R{rng.randint(10, 99)}-{rng.choice('ABCDEFGHJK')}{rng.choice('LMNPQRSTUV')}{rng.randint(100, 999)}"
    if style == 3: return f"GST/{rng.randint(23, 25)}-{rng.randint(24, 26)}/{rng.randint(100, 9999)}"
    if style == 4: return f"TXN{rng.randint(10000000, 99999999)}"
    return f"{rng.choice(['MH', 'DL', 'KA', 'GJ'])}{rng.randint(10, 99)}{rng.randint(100000, 999999)}"


def make_lines(rng, template):
    """Returns (lines, truth). Each line is (text, style, align) with style in body/bold/title/small."""
    vendor = rng.choice(VENDORS)
    bill_date = dt.date(2024, 1, 1) + dt.timedelta(days=rng.randint(0, 900))
    number = bill_number(rng)
    items = [(rng.choice(ITEMS), rng.randint(1, 5), round(rng.uniform(15, 4500), 2)) for _ in range(rng.randint(2, 6))]
    subtotal = round(sum(q * p for _, q, p in items), 2)
    tax = round(subtotal * rng.choice([0.05, 0.12, 0.18]), 2)
    total = round(subtotal + tax, 2)

    num_label = {"receipt": ["Receipt No:", "Receipt #", "Bill No.", "Txn No:"],
                 "invoice": ["Invoice No:", "Invoice Number:", "Inv. No:", "Invoice #"],
                 "utility": ["Bill No:", "Bill Number:", "Account Bill No."],
                 "pharmacy": ["Bill No:", "Receipt No.", "Invoice No."]}[template]
    date_label = {"receipt": ["Date:", "Date"], "invoice": ["Invoice Date:", "Date:", "Dated:"],
                  "utility": ["Bill Date:", "Date of Bill:"], "pharmacy": ["Date:", "Bill Date:"]}[template]
    total_label = {"receipt": ["TOTAL", "Total:", "Grand Total"], "invoice": ["Grand Total:", "Total Amount:", "Amount Payable:"],
                   "utility": ["Amount Payable:", "Total Amount Due:", "Net Amount Payable"],
                   "pharmacy": ["Net Amount:", "Total:", "Grand Total:"]}[template]

    L = []
    L.append((vendor.upper() if template == "receipt" else vendor, "title", "center"))
    L.append((f"{rng.randint(1, 250)}, {rng.choice(['MG Road', 'Station Road', 'Sector 5', 'Ring Road'])}, "
              f"{rng.choice(['Pune', 'Surat', 'Goa', 'Delhi', 'Bengaluru'])}", "small", "center"))
    L.append((f"GSTIN: {rng.randint(10, 37)}ABCDE{rng.randint(1000, 9999)}F1Z{rng.randint(1, 9)}", "small", "center"))
    L.append(("", "body", "left"))
    if template == "invoice":
        L.append(("TAX INVOICE", "bold", "center"))
    L.append((f"{rng.choice(num_label)} {number}", "body", "left"))
    L.append((f"{rng.choice(date_label)} {fmt_date(rng, bill_date)}", "body", "left"))
    if template == "utility":
        due = bill_date + dt.timedelta(days=rng.randint(10, 20))
        L.append((f"Due Date: {fmt_date(rng, due)}", "body", "left"))
        L.append((f"Consumer No: {rng.randint(10000000, 99999999)}", "body", "left"))
        L.append((f"Units consumed: {rng.randint(80, 900)}", "body", "left"))
    if template == "pharmacy":
        L.append((f"Patient: {rng.choice(['A. Sharma', 'R. Mehta', 'K. Iyer', 'S. Khan'])}", "body", "left"))
    L.append(("-" * 34, "body", "left"))
    for name, qty, price in items:
        L.append((f"{name} x{qty}", "body", "left", f"{qty * price:,.2f}"))
    L.append(("-" * 34, "body", "left"))
    L.append(("Sub Total", "body", "left", f"{subtotal:,.2f}"))
    L.append((rng.choice(["GST", "Tax", "CGST+SGST"]) + f" @{int(round(tax / subtotal * 100))}%", "body", "left", f"{tax:,.2f}"))
    L.append((rng.choice(total_label), "bold", "left", fmt_amount(rng, total)))
    L.append(("", "body", "left"))
    L.append((rng.choice(["Thank you! Visit again", "This is a computer generated bill", "E. & O.E."]), "small", "center"))
    if rng.random() < 0.4:
        printed = bill_date + dt.timedelta(days=rng.randint(0, 3))
        L.append((f"Printed on {fmt_date(rng, printed)} {rng.randint(9, 20)}:{rng.randint(10, 59)}", "small", "center"))

    truth = {"bill_no": number, "date": bill_date.isoformat(), "amount": f"{total:.2f}", "vendor": vendor}
    return L, truth


def render(rng, template, lines):
    width = 900 if template == "receipt" else 1650
    family = MONO if template == "receipt" else rng.choice([SANS, SERIF, SANS])
    body = pick_font_name(rng, family)
    bold = pick_font_name(rng, SANS_BOLD) if family is not MONO else body
    size = rng.randint(26, 34) if template == "receipt" else rng.randint(30, 38)
    fonts = {"body": find_font(body, size), "small": find_font(body, int(size * 0.8)),
             "bold": find_font(bold, int(size * 1.05)),
             "title": find_font(pick_font_name(rng, SANS_BOLD), int(size * 1.5))}
    line_h = int(size * 1.55)
    height = 140 + line_h * (len(lines) + 2)
    img = Image.new("L", (width, height), 255)
    draw = ImageDraw.Draw(img)
    margin = 70
    y = 70
    for entry in lines:
        text, style, align = entry[0], entry[1], entry[2]
        right = entry[3] if len(entry) > 3 else None
        font = fonts[style]
        if not has_glyph(font, "\u20b9"):
            text = text.replace("\u20b9", "Rs.")
            right = right.replace("\u20b9", "Rs.") if right else right
        if align == "center":
            w = draw.textlength(text, font=font)
            draw.text(((width - w) / 2, y), text, font=font, fill=0)
        else:
            draw.text((margin, y), text, font=font, fill=0)
        if right:
            w = draw.textlength(right, font=font)
            draw.text((width - margin - w, y), right, font=font, fill=0)
        y += int(line_h * (1.3 if style == "title" else 1.0))
    return img


def degrade(rng, img, severity):
    # severity: 0 clean, 1 mild, 2 moderate, 3 heavy
    if severity == 0:
        return img
    scale = {1: rng.uniform(0.7, 0.9), 2: rng.uniform(0.5, 0.7), 3: rng.uniform(0.38, 0.5)}[severity]
    img = img.resize((int(img.width * scale), int(img.height * scale)), Image.BILINEAR)
    angle = rng.uniform(-1.0, 1.0) * {1: 1.0, 2: 2.5, 3: 4.0}[severity]
    img = img.rotate(angle, resample=Image.BICUBIC, expand=True, fillcolor=255)
    blur = {1: rng.uniform(0, 0.5), 2: rng.uniform(0.4, 0.9), 3: rng.uniform(0.7, 1.2)}[severity]
    if blur > 0.05:
        img = img.filter(ImageFilter.GaussianBlur(blur))
    # uneven lighting: dark gradient across the page, and lower contrast
    shade = {1: rng.uniform(0, 25), 2: rng.uniform(20, 55), 3: rng.uniform(45, 85)}[severity]
    contrast = {1: rng.uniform(0.9, 1.0), 2: rng.uniform(0.75, 0.9), 3: rng.uniform(0.6, 0.75)}[severity]
    px = img.load()
    w, h = img.size
    sigma = {1: 4, 2: 10, 3: 16}[severity]
    gx = rng.choice([True, False])
    for yy in range(h):
        for xx in range(w):
            t = (xx / w) if gx else (yy / h)
            v = 128 + (px[xx, yy] - 128) * contrast - shade * t
            v += rng.gauss(0, sigma)
            px[xx, yy] = 0 if v < 0 else 255 if v > 255 else int(v)
    # JPEG artefacts
    import io
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality={1: rng.randint(70, 90), 2: rng.randint(45, 70), 3: rng.randint(30, 45)}[severity])
    buf.seek(0)
    return Image.open(buf).convert("L")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=100)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--out", default="data")
    args = ap.parse_args()

    rng = random.Random(args.seed)
    os.makedirs(args.out, exist_ok=True)
    truth = []
    templates = ["receipt", "invoice", "utility", "pharmacy"]
    for i in range(args.count):
        template = templates[i % len(templates)]
        severity = (i // len(templates)) % 4
        lines, t = make_lines(rng, template)
        img = degrade(rng, render(rng, template, lines), severity)
        sid = f"{i:03d}"
        img.save(os.path.join(args.out, sid + ".png"))
        img.save(os.path.join(args.out, sid + ".pgm"))
        t.update({"id": sid, "template": template, "severity": severity})
        truth.append(t)
    with open(os.path.join(args.out, "truth.json"), "w", encoding="utf-8") as f:
        json.dump(truth, f, indent=1, ensure_ascii=False)
    print(f"Wrote {args.count} samples to {args.out}")


if __name__ == "__main__":
    main()
