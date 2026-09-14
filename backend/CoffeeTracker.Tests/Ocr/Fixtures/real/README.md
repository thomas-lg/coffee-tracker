# Real bag photos

This folder is empty on purpose, and the benchmark runs without it.

The `synthetic/` corpus next door is rendered, so it is reproducible and small enough to
commit. What it cannot tell you is how the pipeline does on an actual bag: rendered type
is too even, the lighting is a CSS gradient rather than a kitchen, and no amount of blur
turns a screenshot into a photograph. Treat a synthetic score as a **regression signal**,
not as an accuracy figure.

Real photographs are what settles an engine question. To add some:

1. Photograph bags the way you actually would — handheld, whatever light is there. A
   dozen is enough to be informative; thirty is better. Include the awkward ones: matte
   black packaging, foil type, a bag that is mostly illustration.
2. Drop the images in this folder.
3. Write a `manifest.json` beside them in the same shape as
   `../synthetic/manifest.json`:

   ```json
   {
     "fixtures": [
       {
         "file": "la-cabra-kirinyaga.jpg",
         "condition": "handheld",
         "expected": {
           "name": "Kirinyaga AA",
           "roaster": "La Cabra Coffee Roasters",
           "origin": "Kenya",
           "roastLevel": "Light",
           "weight": "250g"
         }
       }
     ]
   }
   ```

   `condition` is a free label you choose; scores are grouped by it, so it is worth
   making it mean something (`handheld`, `shelf`, `dark-bag`).

   A `null` expectation is a real assertion, not a blank: it says the parser should leave
   that field alone rather than invent a value. Use it where the bag genuinely does not
   print the field — and note that `roaster` needs positive evidence on the label
   ("Roasters", "Roastery", "Coffee Co"), so a bag that just says "Onyx Coffee Lab"
   should expect `null`.

   `weight` is the **parsed** form, not what is printed: `CoffeeLabelParser` normalises
   the unit, so a bag printed `500 g` expects `"500g"` and one printed `12 oz` expects
   `"12oz"`.

4. Run the benchmark. The scorecard reports `real/*` rows separately from `synthetic/*`,
   so the two never get averaged into one misleading number.

Whether to commit the photos is your call: they are yours, they are not personal data,
and committing them makes the score reproducible for anyone else. If you would rather
not, the folder is already listed in `.gitignore` except for this README.
