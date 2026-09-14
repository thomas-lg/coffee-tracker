# Real bag photos

Nine photographs of actual bags, scored as their own corpus alongside `../synthetic/`.

They are here because the rendered corpus turned out to flatter the pipeline by roughly
a factor of two. Rendered type is too even, the lighting is a CSS gradient rather than a
kitchen, and the bag fills the frame on a plain ground. These do not:

| corpus | score when they were added |
| --- | --: |
| synthetic | ~82% |
| real, handheld | 54% |
| real, matte-black packaging | 60% |
| real, kraft paper | 20% |

Six came out of a live instance's own `photos/` volume, so they are what the scan
endpoint was handed in production. Three were taken for the corpus. Between them
they carry the cases the rendered fixtures cannot: French and Italian labels, a bag that
is mostly illustration, foil type on matte black, and one photograph whose EXIF says it
is rotated.

## They are downscaled, and that was measured

The originals are 12 MP and about 2.5 MB each, or 23 MB for the nine, which is not a
thing to carry in a repository forever. At 1600px on the long edge they come to 1.9 MB,
and the
score moves from **72.6% to 72.3%**: one field on one fixture. The orientation tag is
carried across the re-encode by hand, because it is the whole point of one of them.

## Adding more

Photograph bags the way you actually would, drop the images here, and add an entry to
`manifest.json`:

```json
{
  "file": "some-roaster-some-coffee.jpg",
  "condition": "handheld",
  "expected": {
    "name": "Some Coffee",
    "roaster": "Some Roaster",
    "origin": "Kenya",
    "roastLevel": "Medium",
    "weight": "250g"
  }
}
```

Three rules for the expectations, and they matter more than they look:

- **Write what the bag prints, not what the code can currently find.** The point is to
  measure the gap to the truth. Several entries here expect a roaster the parser has no
  chance at today, because `RoasterKeywordRegex` only knows English words and these are
  French and Italian bags. Those score as *missing*, which is the honest reading.
- **`null` is a real assertion**, not a blank: it says the bag does not print that field
  and the parser should leave it alone. Inventing a value there scores as **wrong**.
- **`weight` is the parsed form, not the printed one.** `CoffeeLabelParser` normalises the
  unit, so a bag printed `250g ℮ / 8.8 oz` expects `"250g"`.

`condition` is a free label you choose, and scores are grouped by it, so make it mean
something: it is how you tell preprocessing that helps dark packaging and hurts glare
from preprocessing that does nothing.

Adding fixtures changes the total, so re-measure `Floor` in `OcrBenchmarkTests` when you
do; it is a property of this corpus, not a constant of the pipeline.
