# TODO — Progression Expanded

> Working notes for open design/balance work. Opened **2026-07-16** after the pinnacle balance pass.
> **Not gitignored** — unlike `CLAUDE.md`, this file is tracked. Keep it free of anything private.
>
> The numbers below are **not estimates**. Every vanilla constant was verified against the
> decompiled tModLoader assembly, and the model is committed at **`.scripts/pinnacle_balance.py`**
> (gitignored, alongside `levelling_algorithm.py`). Run it before arguing with any figure here:
>
> ```
> python3 .scripts/pinnacle_balance.py
> ```
>
> **Everything here is model output, not play-test.** No Build+Reload has confirmed any of it.

---

## 1. P1 — The Vengeance gap

> **⚠️ The table below is the ORIGINAL (2026-07-16, pre-fix) reading, kept because §1a/§1b argue from
> it.** All three pinnacles have since changed. Current numbers are in
> **`.scripts/juggernaut_sustain.py`** (`pinnacle_balance.py` is **stale** — it still encodes the ×4
> leech and Stagger's flat-regen-only sustain, and has not been updated):
>
> | | max life | defense | DPS | sustain | note |
> |---|---|---|---|---|---|
> | **Stagger** | 876 | 188 | ×1.00 | **144/s** | mana pool (438) absorbing the 55% bleed |
> | **Vengeance** | 1392 | 52 | ×1.00–2.00 | **111/s** | leech, ~30% of DPS, scales forever |
> | **Juggernaut** | 986 | 158 | ×1.40 | **65/s** | all flat — fades with world level |
>
> **The ordering inverted.** Stagger went from weakest to strongest sustain; Juggernaut is now last.
> Whether that is *correct* is unmeasured — Juggernaut carries ×1.40 DPS and the hit cap, which no
> HP/s column can express. **None of it has been play-tested.**

**Vengeance is the strongest pinnacle by a wide margin. Stagger — the intended yardstick — is the
weakest of the three.** Juggernaut sits in the middle and is currently the closest thing to a fair
reference point.

Stat block (580 raw life, 50 armour, Ironhide r5, Fortitude r5, Expert):

| | max life | defense | DPS | sustain | sustain (ramped) |
|---|---|---|---|---|---|
| **Stagger** | 876 | 188 | ×1.00 | 16/s | 16/s |
| **Juggernaut** | 986 | 112 | ×1.40 | 0 | 0 |
| **Vengeance** | **1392** | 52 | ×1.00–**2.00** | **108/s** | **432/s** |

Vengeance has the most life, the most damage, and 7–27× Stagger's sustain. Its only deficit is
defense — which, per §3 below, is the stat that decays to irrelevance anyway.

### 1a. ✅ FIXED (2026-07-16): Vengeance's leech was quadratic in the ramp

**Resolved.** The ramp now enters **once**. `VengeanceTalent` contributes to a new
`CombatEffectStats.ConsumableHealingPercent` channel, which only `GetHealLife` (potions) reads;
`LifeLeechApplier` still reads `HealingPercent` alone, so leech scales via the already-ramped
`damageDone` and nothing else. Full-ramp heal is now `0.30 × baseHit × (1+r)` = **×2**, as documented.
Potions keep the ramp — that was the point of a second channel rather than simply deleting the line.

Also fixed in the same pass: **Vengeance's `lifeRegen` amplification was in the wrong hook** and was
therefore near-dead code. It sat in `PostUpdateMiscEffects`, which runs immediately *before*
`UpdateLifeRegen`, so `lifeRegen` held nothing but `StatApplier`'s flat `LifeRegen` case — none of the
gear/buff regen it was meant to scale. Moved to `UpdateLifeRegen`. ⚠️ **This is a buff landing in the
same pass as the nerf**, against the warning below — a deliberate call, but it means the net change to
Vengeance is harder to read in play-test. If the numbers come out strange, untangle this first.

**Consequence to know:** Vengeance no longer raises `LifeLeechApplier`'s 15% per-hit ceiling, so the cap
is now hard unless the player rolls `Healing` gear. Per §1b that cap is near-inert anyway.

<details>
<summary>Original analysis (kept for the record)</summary>

The highest-confidence finding here, and the first thing to fix. The ramp is applied **twice**:

```
VengeanceTalent.ResetEffects:        GetDamage(Generic) *= (1+r)    -> inflates damageDone
VengeanceTalent.PostUpdateMiscEffects: HealingPercent += r*100
LifeLeechApplier.OnHitNPC:           leeched = damageDone * 0.30    <- already ramped
                                     heal = leeched * (1 + HealingPercent/100)   <- ramped AGAIN

  =>  heal = 0.30 * baseHit * (1+r)^2
```

| ramp | heal/proc (120 base hit) | multiple |
|---|---|---|
| 0.00 | 36.0 | ×1.00 |
| 0.50 | 81.0 | ×2.25 |
| 1.00 | **144.0** | **×4.00** |

**`CLAUDE.md` §6 describes this as a 2×** ("Vengeance at full ramp leeches up to 30% of max life per
proc, not 15%"). It is **4×**. The cap-then-amplify order is intended and correct; what was missed is
that `damageDone` is *already* ramped by the time leech takes its 30% of it.

- [x] Decide whether the square is intended. It was not — the docs described a 2×.
- [x] Fix by feeding the ramp in **once**. Took the separate-channel option rather than dropping the
      contribution outright, so potions keep the ramp.
- [ ] ⚠️ **This is a large nerf on its own. Re-measure BEFORE touching the 30% rate**, or the two
      changes will be indistinguishable in play-test. **Still outstanding — the nerf has not been
      play-tested.**
- [x] Update `CLAUDE.md` §6 + §8 (the leech-pool section) once resolved.

</details>

### 1b. Leech is structurally unbeatable by flat regen

Not a tuning artifact — it survives every weapon profile:

| weapon | hit | rate | DPS | leech/s (r=0) | leech/s (r=max) | % of DPS |
|---|---|---|---|---|---|---|
| very fast (dagger) | 40 | 8.0 | 320 | 40 | 160 | 12% |
| fast (sword) | 120 | 3.0 | 360 | 108 | 432 | 30% |
| medium (bow) | 220 | 2.0 | 440 | 132 | 528 | 30% |
| slow (launcher) | 600 | 0.8 | 480 | 144 | 334 | 30% |
| very slow (sniper) | 1200 | 0.4 | 480 | 84 | 167 | 17% |

- Leech ≈ **30% of DPS** and scales with every weapon upgrade **forever**.
- Stagger's regen is **flat** (8/s base, 16/s standing still, 32/s with Second Wind r4) and **never
  scales again**.
- **Break-even: 0.30 × DPS = 32 → DPS ≈ 107.** A copper shortsword clears that. Past it the gap only
  widens.
- `LifeLeechApplier.MaxLeechPerHit` (15% of max life) only binds when `hit > 0.5 × maxLife` (≈696
  here) — so the per-proc cap is **nearly inert in normal play**. Don't count on it as a limiter.

- [ ] Consider whether 30% is right even after the square is fixed (would still be ~30% of DPS).
- [ ] `RarityInfo.DropChanceMultiplier`-style dead-stat check: is the 15% cap doing *anything*? If it
      only fires on sniper-tier weapons, it may be a false sense of safety.

---

## 2. ✅ IMPLEMENTED (2026-07-16): Stagger — mana as the stagger pool ("Mind over Matter")

**Shipped, not play-tested.** 55% of damage taken still becomes a 5.5s bleed; each *tick* of that bleed
is now paid from **mana** before life. Every open question below was resolved — see "Resolutions".

**One correction to the section header's original framing:** this did **not** replace the flat
`LifeRegen +8/s`. That was kept (see Resolutions), so Stagger is net stronger after this pass.

### Resolutions (all decided 2026-07-16, all now in code)

| Question | Answer |
|---|---|
| Where mana enters | Mana pays the **bleed ticks**. Pool, bleed, `StaggerInstance` and the re-entrancy guard all survive. |
| Mana pool size | `mana.Base += 0.5f * statLifeMax2` in `StaggerTalent.ModifyMaxStats` — **on top of** vanilla's 200. Intellect and mana crystals stay live. |
| Does the bleed survive | **Yes** — required by the crit purge, which needs a pool to purge. |
| At empty | **Cliff.** `statMana <= 0` in `ModifyHurt` → no split, hit lands whole. Binary on *existence*: 1 mana buys a full split. An *existing* bleed still resolves against life. |
| `manaRegenDelay` on hit | **Yes**, in `PostHurt`, from vanilla's own `maxRegenDelay`. |
| Flat regen | **Kept.** Stagger was the weakest pinnacle; regen still answers the 45% that lands now, plus vanilla DoTs which bypass the pool. |

### Verified against the assembly (these corrected the plan)

- ⚠️ **`Player.CheckMana` does NOT set `manaRegenDelay`.** The §2 note below guessed it did. Vanilla sets
  it in **`ItemCheck_ApplyManaRegenDelay`**: `if (GetManaCost(sItem) > 0) manaRegenDelay = (int)maxRegenDelay;`
- **`maxRegenDelay` is recomputed every frame in `Player.Update`**, and is *proportional to how empty you
  are*: `((1 - statMana/statManaMax2) * 240 + 45) * 0.7` → **~31 ticks at full, ~199 at empty**. So it is a
  *second* death-spiral term on top of `num2`. We now mirror it rather than invent a constant.
- **`manaRegenDelay` is a `float`**, not an int.
- **`ResetEffects:94-96`** — `PlayerLoader.ModifyMaxStats(this)` then `statLifeMax2 = statLifeMax`.
  Confirms the §4 trap: inside `ModifyMaxStats`, `statLifeMax2` is **last frame's** value. Harmless here
  (max life only moves on level-up/respec; mana never feeds life, so no loop).
- **`Player.Update` clamps `statMana > statManaMax2` every frame**, so swapping out of Stagger self-corrects
  the now-oversized pool with no work from us. `Player.Spawn` sets `statMana = statManaMax2` — you respawn
  with a full pool, which pairs with `ClearStagger` wiping the bleed on respawn.
- **No div-by-zero guard on `maxRegenDelay`.** `statManaMax2 == 0` → NaN. Irrelevant for Stagger (big pool)
  and pre-existing for Juggernaut, which has no mana to regen. Not fixed.

### 2a. ✅ FIXED (2026-07-16): the mana burn was invisible — vanilla regen swamped it

**Reported in play as "the mana burn isn't being applied" alongside insane tankiness. The burn was
firing correctly; nothing in `StaggerTalent` was wrong.** Model: **`.scripts/stagger_mana.py`** (every
vanilla constant read out of the decompiled assembly, not from memory).

Two *independent* causes. Only the second is fixed:

1. **The damage floor — not fixable by any mana change.** `Player.HurtModifiers.GetDamage:93` does
   `num = Math.Max(num - defense*e, 1f)` **before** `FinalDamage` is applied, then clamps to `>= 1`
   again. With 188 defense at Expert absorbing 141 and raw trash hits of 45–102 until ~WL15, the hit is
   *already 1* when Stagger's ×0.45 runs. Bleed ≈ 1.2 damage ≈ 1 mana. **Below ~WL15 the tankiness is
   the defense, not the pool — testing the mana there will always show nothing.**
2. **Vanilla mana regen — fixed.** `UpdateManaRegen` computes `manaRegen = statManaMax2/3 + 1` **per
   frame**, and 120 accumulated = 1 mana ⇒ **`pool/5.2` mana/s moving, `pool/2.6` planted** = **122 and
   244 mana/s** at the real 638 pool, against a bleed draining 0.2–53/s. Refill from empty: **4.4s**.

> ⚠️ **The premise in §2 was wrong, and this is the part to remember.** Mana was chosen *because*
> vanilla regen is proportional to the pool — and that is exactly what killed it. **Proportional regen
> means the pool always refills in ~5s no matter how big it is**, so `ManaPerMaxLifeFraction` moves
> absorption and refill by the *same factor* and cannot change scarcity at all. It was never the "big
> dial" this section called it. **Only the refill RATE is a dial.**

Two smaller errors compounded it: the §2 model assumed a **438** pool when it is **638** (Stagger's 438
stacks *on top of* vanilla's 200 — `ResetMaxStatsToVanilla` then `ModifyMaxStats`), and it framed the
regen as "earned by disengaging" when **122/s runs while moving and fighting**.

**The fix: `manaRegenBonus -= statManaMax2/3` (`RegenSuppression = 1.0`), from `PostUpdateMiscEffects`.**
Planted regen becomes the *only* regen — 0.5/s moving, 122/s planted.

- **tML has no mana-regen hook** (only `ModifyMaxStats`/`GetHealMana`/`ModifyManaCost`/`OnMissingMana`/
  `OnConsumeMana`), so `Player.manaRegenBonus` (public int) is the seam. Vanilla reads it as a plain
  addend **before** the standing-still term: `manaRegen = pool/3 + 1 + manaRegenBonus;` then
  `if (standing still || grappling[0] >= 0 || manaRegenBuff) manaRegen += pool/3;` — so subtracting the
  base term zeroes the passive drip and leaves planted regen intact.
- **Hook is load-bearing.** `ResetEffects` zeroes it (`:323`); `UpdateManaRegen` (`Update:1637`) runs
  right after `PostUpdateMiscEffects` (`:1634`). Contributing from `ResetEffects` races the wipe.
- **Unconditional, not bleed-gated** — it sits *above* the `instances.Count == 0` early return. Gated,
  the pool would refill at 122/s the instant the last bleed expired: a 5.5s wait for a full reservoir.
- ⚠️ **`RegenSuppression` must stay in [0,1].** At 1, `manaRegen` floors at vanilla's literal `+1`.
  Above 1 it goes negative and `manaRegenCount` drifts negative unbounded (the `while (>= 120)` payout
  never fires), so the pool would owe that debt back before returning a single point.
- **The `num2` spiral now has teeth**: planted regen is 122/s at a full pool but **24/s at an empty
  one**, so bottoming out costs ~8.7s planted to undo. That is the intended minigame, finally live.
- **Two escape hatches kept, both with a real cost:** a Mana Regeneration potion sets `manaRegenBuff`,
  which vanilla treats *exactly* like standing still (buys mobile regen for a buff slot); Mana Potions
  write `statMana` directly and are untouched, so they stay the panic button.
- Pool now lasts **370s @ WL20 → 12s @ WL100** under sustained fire while moving. **Model output, not
  play-test.**

### Still open

- [ ] **Play-test literally all of it.** `ManaPerMaxLifeFraction` (0.50), `CritPurgeFraction` (0.25),
      `CritPurgeCooldownTicks` (60) and now `RegenSuppression` (1.0) are first guesses. **`RegenSuppression`
      is the scarcity dial; `ManaPerMaxLifeFraction` is not — see §2a.**
- [ ] **Is the flat 188 defense the real tankiness problem?** §2a cause 1 says below ~WL15 defense alone
      floors everything to 1 and no talent mechanic is even reachable. Cross-ref §3.
- [ ] **Does the cliff + the twin spirals (20%-at-empty regen AND a ~199-tick delay at empty) make an
      emptied pool unrecoverable mid-boss?** Mana Potions are the intended answer and cost a warrior
      nothing. If it's still unrecoverable, floor the delay rather than weaken the cliff.
      - **Partly self-answering, and worth not breaking.** At 0 mana `ModifyHurt` bails before setting
        `pendingStaggerPercent`, so `PostHurt` early-returns and never reaches the `manaRegenDelay` line —
        meaning **hits taken at an empty pool do not suppress mana regen**. The pool refills (at the 20%
        rate) even under fire, and the first point of mana re-arms the split. This is emergent from the
        early-return structure, not designed; a future refactor that moves the delay earlier in `PostHurt`
        would silently delete the only way out of the cliff.
- [ ] **Does this actually close the §1 gap?** Unmeasured. `.scripts/pinnacle_balance.py` still encodes
      the ×4 leech and Stagger's old flat-regen-only sustain — **update it before trusting §1's table.**

<details>
<summary>Original design notes (kept — the vanilla-behaviour analysis below is still accurate except where corrected above)</summary>

**Chosen direction** (2026-07-16): 55% of damage taken is paid from **mana** instead of life. Stagger
is only "on" while mana exists. Standing still drives huge mana regen. This replaces the flat
`LifeRegen +8/s`, which §1b shows can never scale.

**Why mana:** vanilla already does the hard part, and does it *proportionally* — which is exactly the
property Stagger lacks.

### Verified vanilla mana behaviour (`Player.UpdateManaRegen`)

```
manaRegenDelay -= 1                      # and -1 MORE if standing still / grappling / manaRegenBuff
when manaRegenDelay <= 0:
    manaRegen = statManaMax2/3 + 1 + manaRegenBonus
    if standing still: manaRegen += statManaMax2/3        # <- PROPORTIONAL TO POOL
    num2 = statMana/statManaMax2 * 0.8 + 0.2              # <- 20% rate at empty, 100% at full
    manaRegen = (int)(manaRegen * num2 * 1.15)
manaRegenCount += manaRegen ; every 120 -> +1 mana
```

Four properties fall out for free, none of which we have to build:

1. **Regen is proportional to max mana** (`statManaMax2/3`), so it scales — unlike flat life regen.
2. **Standing still doubles both** the delay tick-down *and* the regen rate. The existing plant stance
   already means something; vanilla just pays it.
3. **`manaRegenDelay` after spending** creates the "crazy regen when not taking damage" window
   natively — *if* we set the delay when mana absorbs a hit.
4. **`num2` is a death spiral**: regen runs at 20% when empty vs 100% when full. Letting the pool
   bottom out is punishing → **that is the resource minigame**, already tuned.

Plus: **Mana Potions** (restore 100) inflict Mana Sickness = `manaSickLessDmg = 0.25f`, which is
**−25% MAGIC damage only**. A melee Stagger tank pays *nothing* to chug them. The panic button
already exists and is already costed against something warriors don't care about.

And the identity lines up: **Juggernaut sets `statManaMax2 = 0`** — mana is *already* the axis that
separates the two walls. This also makes **Intellect** (+2 max mana) a live Stagger stat, where
Juggernaut kills it dead. Three distinct attribute identities from one mechanic.

### OPEN QUESTIONS — resolve before writing code

- [ ] **Mana pool size / how it scales.** Vanilla max mana is **200** (20 + 20×9 crystals) + Intellect
      ×2. That is *tiny* next to 876 life — 55% of a single WL20 hit (raw 165) is ~91, nearly half the
      pool. **The pool must scale off max life or the whole design fails at WL20+.**
      - Proposal: **max mana = 50% of max life** (876 → 438 mana; ~168 mana/s standing still, which is
        comparable to Vengeance's 108–432 but earned by *disengaging* rather than attacking).
      - ⚠️ No stat key exists for "mana as % of life". `StatApplier` has `MaxMana`/`MaxManaPercent`
        (both `ModifyMaxStats`-only, see `CLAUDE.md` §5/§8). This needs **custom code in
        `TalentPlayer.ModifyMaxStats`**, and it must read the *resolved* life — check the
        `statLifeMax` vs `statLifeMax2` trap in §4 below before wiring it.
      - Alternative: a big flat `MaxManaPercent`, but that scales off 200 and off **Intellect only** —
        it would make Int mandatory rather than merely good, and still wouldn't track life investment.
- [ ] **What happens at empty?** Two candidates:
      - (a) **Graceful**: mana absorbs what it can, remainder hits life immediately. Degrades smoothly.
      - (b) **Cliff**: no mana → Stagger is simply off, damage goes to life in full. Matches "stagger
        only active whilst mana exists". More dramatic, more legible fail state, more swingy.
      - Leaning (a) — but (b) is closer to the stated intent. **Needs a decision.**
- [ ] **Does the bleed survive at all?** If mana eats the 55%, is there still a 5.5s DoT? Options:
      mana pays it *instead* of the bleed (bleed deleted entirely — much simpler); or the bleed still
      runs and mana drains to pay it over time (keeps the "window" identity, keeps the instance list).
      **This decides whether `StaggerInstance`/the re-entrancy guard survive.**
- [ ] **Do we set `manaRegenDelay` when mana absorbs a hit?** Almost certainly yes — that's what makes
      "not taking damage" the regen trigger. Confirm the value (vanilla casting uses ~`maxMana/...`,
      check `Player.CheckMana`).
- [ ] **Does `num2`'s 20%-at-empty spiral make (b) unrecoverable?** If the pool empties mid-boss you
      may never get it back. Might need a floor, or might be the intended fail state.
- [ ] **Interaction with the mana-cost masteries.** `arcane_focus` grants `ManaEfficiency` — is that
      now a *defensive* stat for Stagger? Probably harmless, but it's a silent crossover.
- [ ] **Magic builds cannot take Stagger** (mana is their ammo). Currently fine — only
      `warrior_tree.json` exists (`CLAUDE.md` §7) — but it's a door closing. Accept explicitly.
- [ ] **Does this actually close the gap?** ~168 mana/s standing still vs Vengeance's 108–432. It
      competes, but Vengeance still wins on life (1392) and doesn't have to stop moving. **§1 must be
      fixed regardless — this does not substitute for the Vengeance nerf.**

</details>

### Alternatives considered (2026-07-16), kept for the record

Rejected in favour of mana, but all five addressed the same scaling flaw:

| # | Idea | Resource | Why not (for now) |
|---|---|---|---|
| 2 | **Anchor** — planting *deletes* the pool at ~20%/s instead of healing | position/time | Smallest diff, scales fine. Viable as Stagger's **stance** alongside mana rather than a rival. |
| 3 | **Bloodletting** — hits purge stagger (~40% of damage dealt) | DPS | Converges on Vengeance's "attack to sustain" loop, and does it worse (no damage bonus). **Partially adopted 2026-07-16** as the crit purge: 25% of the pool, gated on a **crit** and a **1s cooldown**, which sidesteps the objection — it's a burst clear on a timer, not a per-hit drip that scales with weapon speed. |
| 4 | **Grit** — a brace keybind; can't move/attack, pool drains ~25%/s | attack uptime | Cleanest cost, but invents a verb Terraria doesn't have. Highest effort. |
| 5 | **Riposte** — only hitting the NPC that staggered you purges it (×3) | target discipline | Great tank flavour (taunt without a taunt). Miserable vs swarms; needs per-instance `whoAmI` + liveness. |

---

## 3. Why Stagger's defense decays (context for §2)

> ⚠️ **THE TABLE IN THIS SECTION IS WRONG — see §3a (2026-07-16).** It models Classic-mode base
> damage at Expert's defense effectiveness and applies only **one** of the two world-level damage
> terms. Its headline finding ("WL1–10 Stagger is literally immune to trash") is an **artifact of the
> model, not the game**. The prose below about *subtractive defense vs multiplicative damage* is still
> correct and is in fact the whole problem; only the numbers are junk. Use
> **`.scripts/enemy_damage_scaling.py`**.

`DefensePercent` compounds correctly (it's a `Player.DefenseStat` `FinalMultiplier`) and Stagger's
×2.5 is genuinely huge — **188 defense**, which at Expert (`e=0.75`) absorbs **141 damage flat**. But
defense is **subtractive**, and the mod scales enemy damage **linearly and without a cap**:

```
WorldLevelManager.GetEnemyDamageMultiplier() = 1 + (WL-1)*0.14     # WL100 -> x14.86
NPCLevelManager: npc.damage *= dmgMult * (1 + levelDiff*0.04)
```

| WL | dmg mult | raw hit | Stagger takes | % absorbed |
|---|---|---|---|---|
| 1 | ×1.00 | 45 | **1** | 100% |
| 10 | ×2.26 | 102 | **1** | 100% |
| 20 | ×3.66 | 165 | 23 | 85% |
| 30 | ×5.06 | 228 | 86 | 62% |
| 50 | ×7.86 | 354 | 212 | 40% |
| 100 | ×14.86 | 669 | 527 | 21% |

**WL1–10 Stagger is literally immune to trash** (floored to 1 damage). By WL30 it's absorbing <62%,
by WL100 <22%.

**The root cause is bigger than Stagger:** player max life is effectively **capped** (500 from
crystals+fruit, +198 from all 99 points in Strength, ×1.2 Fortitude) while enemy damage scales
**×14.86**. Nothing flat — not regen, not defense — can hold that line. Any Stagger fix must be
proportional.

- [x] **Broader question: is 14%/level uncapped enemy damage right at all?** — **Answered: no, and it
      is worse than 14%.** See §3a. The real bug was here, not in the talents.
- [ ] Related, already logged in `CLAUDE.md` §7 P2: **compounding enemy HP** (world-level multiplier
      applied twice, then rarity ×3, then Juggernaut ×2). Same family of problem — and §3a shows the
      **damage line has the identical double-count**, which was never logged.

---

## 3a. ✅ IMPLEMENTED (2026-07-16) — Enemy damage was over-scaled ~6.6× at WL8; §3's model hid it

> Opened **2026-07-16** from a play-test report: *"at WL8 Master + For the Worthy, things are still
> doing incredible amounts of damage even with Stagger and 50 defense."* They were right. Model:
> **`.scripts/enemy_damage_scaling.py`**. Every vanilla constant re-verified against the decompiled
> assembly — the numbers here are **not** the guesses §3's were.

### The stack, WL8, deep cavern, Common Zombie (Classic base damage 15)

| step | source | mult | running |
|---|---|---|---|
| vanilla Classic base | — | — | 15 |
| `NPC.ScaleStats_ApplyGameMode` | Master **3** + FTW **1** | **×4** | 60 |
| `ApplyLevelScaling` world term | `1 + (8-1)*0.14` | ×1.98 | 118 |
| `ApplyLevelScaling` npc-level term | `1 + (14-1)*0.04` | ×1.52 | **180** |
| `EnemyModifierSystem` rarity | Uncommon (20% of spawns) | ×1.3 | 234 |
| `ModifyHitPlayer` level-diff | `1 + 0.20*(14-8)` | ×2.20 | *after defense* |

**×26.5 vs Classic vanilla; ×6.6 of that is ours, stacked on top of a ×4 vanilla already applied.**
Mythic rarity takes it to ×79.5. A 50-armour Stagger tank (125 defense) takes **121** per Zombie hit
and **519** per Angry Bones hit, against ~200–320 max life at level 8.

### Three separate findings

1. **⚠️ The world level is charged TWICE — this is a bug, not tuning.**
   `ApplyLevelScaling` multiplies by `GetEnemyDamageMultiplier()` (a function of world level) **and**
   by `(1 + (npcLevel-1)*0.04)` — but `npcLevel = worldLevel + offset`, so that is *also* world level.
   **This is the exact double-count `CLAUDE.md` §7 P2 already calls a bug on the HP side.** Nobody
   logged the damage line, which has the identical shape. Inflation vs the intended 14%/level:
   ×1.52 @ WL8, ×2.0 @ WL20, ×3.2 @ WL50, **×5.2 @ WL100**. Neither term is capped, so it compounds.
2. **The level-difference system is not a level-difference system — it is a depth tax.**
   `IncreaseWorldLevel(1)` fires on every player level-up (`PlayerLevelManager.cs:162`, and
   `LevelManager.SyncWorldLevelToPlayers` is **dead code** — no callers), so `playerLevel == worldLevel`
   by construction. The difference `npcLevel - playerLevel` therefore reduces to **the depth/biome
   offset alone**, which `InitializeLevel` biases upward and never downward. So it is a flat ×2.2 for
   being in a cavern, permanently, and the player's counterpart (`GetPlayerDamageMultiplier`, +10%/lv)
   only fires when the enemy is *below* them — which underground never happens. Every part of the
   asymmetry points one way: enemies get **20%**/level and cap at **5×**; the player gets **10%**/level
   and caps at **3×**.
   - ⚠️ **Hazard:** world level is per-world and append-only, so a *second* character in the same
     world pushes `worldLevel` above `playerLevel` and turns the depth tax into a permanent tax.
3. **There is no stable middle band, because subtractive defense meets multiplicative damage.**
   At WL8 vs a Zombie: **50 armour → 121 damage taken; 70 armour → 11.** A 40% armour swing is a 10×
   damage swing. Below the threshold you die in two hits, above it you take 1 and are immune. This is
   why it reads as "am I bad?" — there is nothing to *play* between those two states.

### What §3 got wrong (and what it cost)

§3's table applied **no difficulty multiplier at all** (Classic ×1, never Master's ×3 or FTW's +1),
used **Expert** defense effectiveness (0.75) against a **Master** report (1.0), modelled **one** of the
two world-level terms, and included neither rarity nor the level-diff multiplier. Recomputed:

| WL | §3 claimed | actual (Master+FTW, 50 armour, deep cavern) |
|---|---|---|
| 5 | 1 | 13 |
| 8 | 1 | **121** |
| 10 | 1 | **200** |
| 15 | 1 | **426** |
| 20 | 1 | **690** |

**This is what made §2a's "the mana burn is untestable below WL15" conclusion look true.** It was not
— the burn was reachable the whole time. §2a's *cause 1* ("the damage floor") is real but fires far
lower than WL15, so that section's advice to not bother testing mana at low WL should be ignored.

### Verified vanilla constants (new — none of these were in §4)

- **`GameModeData.MasterMode.EnemyDamageMultiplier = 3f`** (`EnemyMaxLifeMultiplier = 3f`);
  Expert `2f`/`2f`.
- ⚠️ **For the Worthy is `+1` to the multiplier, not a separate stage.**
  `ScaleStats_ApplyGameMode` does `damage = (int)(damage * (EnemyDamageMultiplier + num2))` where
  `num2 = 1` if `Main.getGoodWorld`. **So Master+FTW is ×4, not ×3** — and Classic+FTW is ×2.
- **`NPC.NewNPC:91956` calls `SetDefaults(Type)` BEFORE `NPCLoader.OnSpawn` at `:91980`.**
  `SetDefaults` → `ScaleStats` → `ScaleStats_ApplyGameMode`. So `npc.damage` **already contains
  vanilla's difficulty multiplier** when our `OnSpawn`→`ApplyLevelScaling` runs. We are multiplying
  on top of it, always.
- **`Player.HurtModifiers.GetDamage:249`** — defense subtracts at `:255`, `FinalDamage` applies at
  `:256`. So our `ModifyHitPlayer` multiplier scales the **post-defense remainder** (this is the
  player-friendly ordering: it amplifies what defense absorbed too). `IncomingDamageMultiplier` would
  have been much worse — **do not "fix" it to that**.
- **Contact damage** = `Main.DamageVar(npc.damage * damageMultiplier, -luck)` (`Player.cs:29762`),
  ±15% (`Main.DefaultDamageVariationPercent = 15`, `:87341`). `npc.damage` is used directly.

### Correction: the real numbers are worse than the table above

The report said **"50 defense in the in-game tooltip"**, not 50 armour. Stagger's ×2.5 is a
`Player.DefenseStat` FinalMultiplier, so it is **already inside** that number — the armour underneath
is ~20. The §3a table above modelled 50 *armour* → 125 defense, i.e. **2.5× too generous**. At the
real 50 defense, WL8, deep cavern, Master+FTW, **before Calamity/Fargo's**:

| enemy | base | raw | total taken | hits to die (~230 life) |
|---|---|---|---|---|
| Zombie | 15 | 180 | **286** | **0.8 — one-shot** |
| Skeleton | 20 | 240 | 418 | 0.6 |
| Angry Bones | 30 | 361 | 684 | 0.3 |
| Undead Miner | 35 | 421 | 816 | 0.3 |

### What shipped

| | was | now |
|---|---|---|
| `WorldLevelManager.GetEnemyDamageMultiplier()` | `1 + (WL-1)*0.14`, uncapped | `min(1 + (WL-1)*0.03, 2.5)` |
| `ApplyLevelScaling` per-NPC term | `npcLevel - 1` (double-count) | `LevelOffset()` = `npcLevel - worldLevel` |
| `ApplyLevelScaling` damage term | `× (1 + (npcLevel-1)*0.04)` | **deleted** |
| `NPCLevelManager.GetEnemyDamageMultiplier` | 20%/lv, cap ×5.0 | 5%/lv, cap ×1.5 |

| our damage mult (deep cavern) | WL8 | WL20 | WL50 | WL100 |
|---|---|---|---|---|
| before | ×6.62 | ×16.10 | ×55.33 | ×170.00 |
| **after** | **×1.57** | **×2.04** | **×3.21** | **×3.25** |

Zombie at 50 defense, WL8: **286 → 28** (vanilla Master+FTW with no mod at all: 10).
Angry Bones: **684 → 123** (vanilla: 70). WL20 Zombie: 855 → 57.

**Three reasons damage got its own rate rather than sharing health's 15%:** health is *friction*
(answered by DPS, which grows ~1000× — huge headroom, so 15% is kept), damage is *lethality*
(answered by EHP, which grows ~10× at best); vanilla's roster **already** escalates damage ×12 across
a playthrough, so a big multiplier here stacks a second progression curve on vanilla's; and the player
gains ~1%/level against 14%/level, which diverges forever. Full reasoning in `CLAUDE.md` §12.

**HP rode along** (same variable): WL8 HP multiplier ×3.38 → ×2.67, a ~21% reduction. That is the
`CLAUDE.md` §7 P2 bug, fixed. Revert by reading `npcLevel - 1` for health only, if it is missed.

### Still open

- [ ] **Play-test it.** `ENEMY_DAMAGE_PER_LEVEL` (0.03), `ENEMY_DAMAGE_MAX_MULTIPLIER` (2.5),
      `ENEMY_DAMAGE_PER_LEVEL_ABOVE` (0.05) and its ×1.5 cap are **all first guesses**, and this is
      four changes in one pass — worse than the five that §5a already flagged as unreadable.
      Mitigation: **the double-count fix and the 14%→3% rate are ~90% of the swing**; the level-diff
      softening is garnish. Move the rate first if it is wrong.
- [x] ✅ **Calamity/Fargo's investigated — they add nothing for trash, and this was the surprise.**
      Both `.tmod`s extracted and decompiled. **Calamity Death Mode and Fargo's Masochist Mode are UI
      wrappers that force `Main.GameMode = 2` (vanilla Master, ×3).** Neither has a universal
      hostile-damage multiplier: Calamity's `RevDeathStatChanges` is a per-**boss** switch, and
      Fargo's `EModeGlobalNPC` only does `value *= 1.3` plus a trash `lifeMax *= 1.1` that is gated
      on `!Main.masterMode` and therefore **inactive in Masochist**. Their bosses are wholesale AI
      replacements, not scaled vanilla. Details in `CLAUDE.md` §12.
      - **So the numbers above were over-stated, not under-stated.** The model assumed Master **+ FTW
        (×4)**; `getGoodWorld` is a **seed** flag independent of GameMode, and "Death/Maso mode" does
        not imply it. At the ×3 that actually applies: WL8 Zombie **187** (not 286), Angry Bones
        **484** (not 684). Still a two-shot and a one-shot respectively through Stagger.
      - ⚠️ **The real lesson: our ×6.62 was more than twice the entire rest of the difficulty stack
        combined.** The instinct that a big unknown multiplier was hiding in the modpack was wrong,
        and it is exactly how our own numbers escape scrutiny. **Confirm `Main.getGoodWorld` on the
        actual world before trusting any absolute number here.**
- [ ] ⚠️ **`FargoSoulsPlayer.ApplyDR()` caps `Player.endurance` at 0.75 in Eternity Mode**, and our
      `Endurance` stat key writes that field — so every Endurance source in the mod is capped at 75%
      total on this modpack. Probably fine (nothing gets near it), but it is unrecorded elsewhere and
      would silently eat an Endurance-stacking build.
- [ ] **The knife edge is NOT fixed, and it is the deeper problem.** Subtractive defense vs
      multiplicative damage means there is no middle band: vanilla floors damage to 1 *before*
      `FinalDamage` (`GetDamage:255`), so the top of the curve is a cliff, not a slope. **More
      player-side mitigation cannot fix this** — it only moves which world level the cliff sits at.
- [ ] **Decide what `ModifyHitPlayer`'s multiplier is FOR.** It cannot be a level-difference mechanic
      while `playerLevel == worldLevel` by construction. Either own it as a depth tax (rename it), or
      decouple world level from player level so the difference means something again. The latter is
      the more interesting design — it would let the player *out-level* a region.
- [ ] **Rarity is still uncapped on top**: Mythic ×3 on damage AND health, stacking with everything
      above. At WL8 a Mythic Angry Bones is still ×3 of the numbers above.

---

## 4. Traps found while doing this (don't re-learn these)

- ⚠️ **`statLifeMax` is NOT the pre-bonus base.** `Player.ResetEffects` runs
  `PlayerLoader.ModifyMaxStats` **first** — which resets `statLifeMax` to the vanilla base
  (`100 + crystals*20 + fruit*5`) and applies **every** ModPlayer's contribution to it — and only
  *then* does `statLifeMax2 = statLifeMax`. So **both already contain** the talent's own bonuses.
  `statLifeMax2` differs only by vanilla's own on-top bonuses. (`Player.cs:17200`,
  `PlayerLoader.cs:439/451`.) Now documented in `CLAUDE.md` §6.
- **`Player.HurtModifiers.SetMaxDamage(limit)`**: `_damageLimit = min(_damageLimit, max(limit,1))` —
  lowest wins across mods (order-independent), floors at 1, and `GetDamage` clamps it **last**, after
  defense/armour-pen/`FinalDamage`. This is why Juggernaut's cap composes safely.
- **`DefenseEffectiveness`**: Classic **0.5** / Expert **0.75** / Master **1.0**. Balance conclusions
  flip between difficulties — always state which you mean.
- **Every % damage key is "more", not "increased"** (already in `CLAUDE.md` §8) — so Juggernaut's
  `GenericDamage 0.65` is ×1.65 compounding with gear, and its **effective** DPS is ×1.65 × 0.85
  (attack-speed penalty) = **×1.40**.
- ⚠️ **`HurtModifiers.GetDamage` floors damage to 1 BEFORE `FinalDamage` runs**, then clamps `>= 1`
  again: `num = Math.Max(num - defense*e, 1f); return Math.Clamp((int)FinalDamage.ApplyTo(num), 1, _damageLimit);`
  So any `FinalDamage` multiplier is dead against a hit that defense has already floored — which is why
  Stagger's 55% split does nothing below ~WL15 (§2a).
- **Vanilla mana regen is per-FRAME and proportional**: `manaRegen = statManaMax2/3 + 1 + manaRegenBonus`,
  `manaRegenCount += manaRegen` each frame, 120 → 1 mana. So **mana/s = manaRegen/2** and a pool refills
  in ~5s (moving) / ~2.6s (planted) *at any size*. **`manaRegenBonus` is a plain addend applied before
  the standing-still term** — the only lever, since ModPlayer has no mana-regen hook.
- Decompiling: `ilspycmd` needs .NET 6 and fails here. Use `ICSharpCode.Decompiler` (8.2.0.7535) as a
  library from a net8 console app, and **dump output outside the probe project dir** or the `.cs`
  gets compiled into it. `Assembly.LoadFrom` on `tModLoader.dll` fails on the FNA dependency — use
  `MetadataLoadContext` with a `PathAssemblyResolver` for reflection-only inspection.
  - **For `CSharpDecompiler` specifically** (2026-07-16, working recipe): the single-arg
    `new CSharpDecompiler(path, settings)` ctor throws `ResolutionException` on FNA. Build a
    `UniversalAssemblyResolver(asm, throwOnError: false, targetFramework: null)`, `AddSearchDirectory`
    the tModLoader install dir (+ subdirs), and pass `new CSharpDecompiler(new PEFile(asm), resolver, settings)`.
  - **Nested types**: `FullTypeName` wants `Terraria.Player+HurtModifiers`, but `DecompileTypeAsString`
    still fails on it — look the type up in `TypeSystem.MainModule.TypeDefinitions` and call
    `DecompileAsString(type.MetadataToken)` instead.

---

## 5. Also outstanding (cross-ref `CLAUDE.md` §7/§9)

- [ ] **Juggernaut's 50% hit cap (added 2026-07-16) is a pure buff with no compensating cost taken.**
      First hard *guarantee* in the slot. Model says it rarely binds vs trash until ~WL95 (its own
      +50% life raises the cap alongside it) but is a hard 2-hit floor vs boss-tier hits. Watch it.
      If it over-delivers, move the **costs**, not the cap — weakening the cap deletes the guarantee
      that is its entire point.
      - **Note the hit cap makes max life nearly irrelevant to hits-to-die-from-full.** The cap is 50%
        of max life *by construction*, so a capped hit always takes exactly half the bar and it is
        always exactly 2 hits, whatever the total. Life only decides the sub-cap hits and the DoTs.
        This is why "convert Strength's life into regen" was cheaper than it looked — and it was
        rejected anyway, see below.

## 5a. ✅ IMPLEMENTED (2026-07-16): Juggernaut's sustain

**Juggernaut had no sustain — 3.3 HP/s (vanilla potions) vs Stagger's 144 and Vengeance's 111.** Not
merely behind: the only pinnacle with *no* way to recover life, and −50% movement means it cannot
disengage onto the natural regen ramp either. Model: **`.scripts/juggernaut_sustain.py`**.

Five levers landed together → **65.3 HP/s**, 158 defense. **Still last of the three.**

| # | Change | Worth | Dial |
|---|---|---|---|
| 1 | Strength → **+1 HP/s regen** per point | +40 HP/s | `RegenPerStrength` |
| 2 | Strength → **+1% defense** per point | 112→158 def | `DefensePercentPerStrength` |
| 3 | Potion cooldown **×0.25** (60s→15s, 11s w/ pStone) | ~20 HP/s | `PotionDelayMultiplier` |
| 4 | Potion healing **+50%** (200→300) | in #3 | `PotionHealingBonus` |
| 5 | A potion **doubles life regen for 4s**, decaying | +5.3 HP/s | `PotionRegenBonus` |

**Decisions taken:**
- **Additive Strength, not conversion.** Strength keeps +2 life and *also* grants regen+defense, so
  it does strictly more for a Juggernaut than for anyone else — which blunts the talent's stated
  price (dead Dex/Int against a breadth gate that forces ~50 points anyway). Taken knowingly: the
  model says Juggernaut finishes **last** on sustain even with the generous version, so there was no
  headroom to charge it more. It makes the pick viable, not strong.
- **It fixes the mid-game and deliberately does nothing late.** WL20: dies in 12.9s → **survives
  indefinitely**. WL30: 7.1s → 22.6s. WL75+: unchanged. Every number is **flat**, so it fades exactly
  as §3 says flat things fade. Accepted — the hit cap is Juggernaut's late-game answer; this is for
  the part of the curve where the hole was.
- **DoTs switch the whole regen engine off, on purpose.** Strength's regen is a flat add in
  `PostUpdateMiscEffects`, landing before vanilla's `if (lifeRegen > 0) lifeRegen = 0` debuff block —
  same as Stagger's. DoTs already bypassed the hit cap; now they cut the sustain too, and **potions
  are the counter-play because a direct heal is not regeneration**.

**Verified against the assembly (these shaped the code):**
- `Item.potionDelay = 3600` / `restorationDelay = 2700` / `mushroomDelay = 1800`;
  `PhilosopherStoneDurationMultiplier = 0.75`. tML's own docs name `Player.PotionDelayModifier` as the
  sanctioned Philosopher's-Stone lever.
- ⚠️ **`PotionDelayModifier` MUST be contributed from `ResetEffects`.** `Player.Update` saves the old
  value (`:22738`), resets (`:22742`), applies pStone (`:22745`), calls `ResetEffects()` (`:22750`),
  then at **`:22892`** compares old vs new and calls `AdjustRemainingPotionSickness` if they differ.
  `PostUpdateEquips` (`:22942`) and `PostUpdateMiscEffects` (`:23190`) are both **after** it —
  contributing there makes the rescale fire *every frame* and ×4 the remaining Potion Sickness each
  time. Permanent compounding sickness.
- ⚠️ **`GetHealLife` is not a "potion drunk" event** — vanilla calls it speculatively from
  `QuickHeal_GetItemToUse` (`:6332`) while merely *choosing* a potion. The event is tML's
  `ApplyPotionDelay` veto hook, safe to use as one because `PlayerLoader.ApplyPotionDelay` does
  `flag &= hook(...)` with **no short-circuit**. Gated on `item.healLife > 0`, not `item.potion` —
  vanilla reaches it from the QuickMana path too (`:6370`).
- `PostUpdateMiscEffects` (`:23190`) runs immediately before `UpdateLifeRegen()` (`:23191`);
  `PlayerLoader.UpdateLifeRegen` (`:17305`) fires *after* the debuff-zeroing block (`:17120`+).
  `lifeRegen = 0` happens in `Player.ResetEffects` (`:16569`).

**Still open:**
- [ ] ⚠️ **Five changes in one pass** — worse than the two that already made Vengeance unreadable
      (§1a). Mitigation: **Strength's regen is ~86% of the total**, so if the result is wrong, move
      that first and treat 2–5 as garnish.
- [ ] **Play-test all of it.** Every constant is a first guess.
- [ ] **Is "still last on sustain" actually wrong?** Juggernaut carries ×1.40 DPS and the hit cap,
      neither of which an HP/s column expresses. Do not chase parity on that number alone.
- [x] ~~**Stagger's `StandingStillRegenMultiplier` (×2)** is moot if §2 lands~~ — **not moot: the flat
      regen was KEPT** when §2 landed, so the multiplier still does its job. See §2 Resolutions.
- [ ] **`StatApplier.cs:125-131` (`LifeRegenPercent`) has the same wrong-hook bug Vengeance just had** —
      it amplifies `lifeRegen` from `PostUpdateMiscEffects`, which runs *before* `UpdateLifeRegen`
      populates it with gear/buff regen. So every `LifeRegenPercent` source in the mod (masteries, item
      rolls) is probably scaling almost nothing. Vengeance's copy was moved on 2026-07-16; this one is a
      wider refactor touching the mastery tree, so it was left alone. **Likely a live bug affecting real
      player stats — worth confirming early.**
- [ ] **`EvilTalents.cs:117` (Devourer's kill-burst) calls `LifeLeechApplier.Heal` directly**, which
      never reads `HealingPercent`. Vengeance's comment used to claim the channel covered it; the comment
      is now corrected, but decide whether that's a doc bug or a missing multiply.
- [ ] Play-test literally any of this. **Zero of it has been Build+Reload'd.**

---

## 5b. ✅ IMPLEMENTED (2026-07-17): the pinnacle defense spread was the bug

> **First real play-test of the pinnacle slot.** Reported: *"every tank apart from Stagger feels
> incredibly squishy. Against white mobs they all do fine, which is perfect. Against bosses, Stagger
> is the only one that actively has a chance against even King Slime."* Both halves were correct, and
> the cause was structural. Model: **`.scripts/mitigation_model.py`** (+ `.scripts/juggernaut_stuck.py`
> for the Juggernaut-specific "stuck in a boss" case).

### The finding: a defense SPREAD in a subtractive system is a cliff, not a gradient

The three pinnacles were spread across `DefensePercent` from **×0.7 (Vengeance) to ×2.5 (Stagger)**.
Defense is **subtractive**, and vanilla floors damage to 1 **before** `FinalDamage`
(`HurtModifiers.GetDamage:255`), so that spread did not produce a proportional outcome:

| pinnacle | defense | WL8 | WL20 | WL50 | vs Stagger @WL20 |
|---|---|---|---|---|---|
| Stagger | 188 | **1** (immune) | 62 | 238 | reference |
| Juggernaut | 157 | 31 | 101 | 277 | 1.6× |
| Vengeance | 52 | **168** | 238 | 413 | **3.8×** |

A 3.6× stat gap became a 3.8× damage gap at WL20 and an **unbounded** one at WL8, where Stagger's 188
defense simply exceeded the raw hit. Against trash everyone clears the floor and everyone is fine —
which is exactly why the complaint was **boss-only**. *The spread was the bug, not the numbers.*

### What shipped

| | was | now |
|---|---|---|
| Stagger `DefensePercent` | `1.50` (×2.5) | **removed** |
| Juggernaut `DefensePercent` | `0.50` (×1.5) | **removed** (Strength's +1%/point **kept**) |
| Vengeance `DefensePercent` | `-0.30` (×0.7) | **removed** |
| — | — | **`ClassBaselines.WarriorEndurance = 0.50`**, gated on `PlayerClass.Melee` |
| — | — | **`ClassBaselines.WarriorKnockbackTaken = 0.50`** — knocked back half as far |
| Vengeance `WindowSeconds` | `4f` | **`10f`** |

**Spread collapses 3.8× → 1.2×, at *every* endurance value** — the compression comes from the
*removal*, not the number, so the two decisions are independent. Trash still takes 1 damage, so the
"white mobs are fine" property survives untouched. The pinnacles are now separated by their
**mechanics** (mana pool / hit cap / leech ramp) rather than by a stat, which is what §6 says the slot
is for.

- **Why 0.50 and not lower:** each pinnacle needs a different endurance merely to break even —
  Stagger **~70%**, Juggernaut **~40%**, Vengeance better at any value (it lost a *penalty*).
  ⚠️ **Below ~40% Juggernaut comes out WORSE than before**, which is half of what this fixes.
  At 0.50: Juggernaut **+19%**, Vengeance **+2.3×**, Stagger **−1.7×**.
- **Why not 0.70** (which would nerf nobody): it consumes the whole of Fargo's 0.75 endurance clamp,
  making every Endurance gear roll and the `Endurance` stat key **dead** on this modpack. 0.50 leaves
  25 points of headroom.
- **Vengeance's 4s→10s is arithmetic, not taste.** A sliding window drops a hit after `WindowSeconds`,
  so a 3-second disengage costs `3/window` of the ramp: **exactly 75% at 4s**, 30% at 10s. The report
  said "you miss 75% if not more" — **the model and the play-test independently agree on the
  mechanism.** The ramp is anti-synergistic by construction: it *builds* while you are hurt but only
  *pays out* while you attack, so it peaks exactly when you most need to leave.

### Still open

- [ ] **Play-test all of it. Zero Build+Reload.** `WarriorEndurance` (0.50) and
      `WarriorKnockbackTaken` (0.50) are first guesses.
- [ ] **Knockback reduction is unmodelled.** Unlike everything else in §5b it has no numbers behind it
      — knockback distance is not something `.scripts/mitigation_model.py` simulates. It should quietly
      help the "stuck in a boss" case (less juggling = more control, which matters most to a Juggernaut
      already paying −50% movement), but that is reasoning, not measurement. **Watch whether it makes
      Juggernaut's mobility cost feel cheaper than intended.**
- [ ] ⚠️ **This knowingly nerfs Stagger, the yardstick, by 1.7×.** It should still lead — its mana pool
      eats 55% of every hit before life is touched, leaving it at ~47 effective vs the others' 85–104 —
      but that lead is now *earned by a mechanic that drains* rather than handed over by a stat.
      **If Stagger is now too weak, move `ManaPerMaxLifeFraction`. Do NOT put the defense back** — it
      brings the cliff with it.
- [ ] ⚠️ **Vengeance's 10s window is a DAMAGE buff too**, not only a leech-uptime fix — the ramp
      multiplies `GetDamage`, so peak uptime rises on the offensive half as well. Deliberately not
      paired with a `DamagePerLifeFraction` cut (the "spikey" feel is the point). If Vengeance now
      over-delivers on damage, `WindowSeconds` is the first thing to look at.
- [ ] ⚠️ **Two big changes in one pass again** (§1a/§5a's recurring lesson). The endurance and the
      window are separable and touch different talents, so this is more readable than the five-lever
      Juggernaut pass — but Vengeance got *both*, so if Vengeance is wrong, move the window first.
- [ ] **`PlayerClass` is now mechanically load-bearing for the first time** (`CLAUDE.md` §8 said it
      "has no other mechanical effect yet" — that is now false). Only `warrior_tree.json` exists and
      every class opens it, so a non-Melee selection currently grants **no endurance at all** while
      still opening the warrior tree. Harmless today; a trap when Ranged/Magic/Summoner land.
- [ ] **Ironhide (`warrior_tree.json`, `DefensePercent 0.10`/rank) was NOT removed.** It is a player
      investment that applies equally to all three pinnacles, so it does not create a spread — but it
      is 50 of the 75 baseline defense the model assumes, and it still feeds the cliff. Decide whether
      "removing any touch to defensives" was meant to include it.
- [ ] **Bone Armor converts defense→damage** and was balanced against Stagger's ×2.5. With the ×2.5
      gone its offence is quietly nerfed too. Unmeasured.
- [ ] ⚠️ **Fargo's `ApplyDR()` clamp interaction is unverified.** It clamps `Player.endurance` to 0.75.
      We contribute from `PostUpdateMiscEffects`; if Fargo clamps in an *earlier* hook, our 0.50 lands
      on top of an already-clamped value and could reach the `> 1f` guard — which would be
      near-invulnerability. **Verify which hook Fargo clamps in before trusting this.**

---

## 6. The elemental system

> **This section exists because there wasn't one.** Every other system in the mod has its open
> constants tracked here; the elemental system had *nothing*, despite `CLAUDE.md` §10 shipping in
> commit `9f2f6cf` with four untested first guesses and no mitigation of any kind. Opened
> **2026-07-16** alongside Phase 2. Model: **`.scripts/elemental_resistance.py`**.

### 6a. ✅ IMPLEMENTED (2026-07-16): Phase 2 — resistance

Shipped, **not play-tested**. Resistance is now a **rolled property**, not a species trait: a
per-enemy per-element triangular roll (mean +0.15, half-width 0.25), four stateless ward affixes
(+0.40 to one element, weight 50), and Bleed's armour curve `def/(|def|+50)`. Five masteries at rank 4
go from **~160% → ~133%** of direct DPS. Full write-up in `CLAUDE.md` §10.

**Two of §10's five planned sources were cut, both deliberately:**

- ⚠️ **`buffImmune` is a trap and §10's rationale for it was factually wrong.** Verified twice against
  the assembly. It encodes "should this icon appear / would this break the AI", not elemental affinity;
  it is wildly asymmetric (Fire 45 / Cold 25 / Venom 2 / **Electrified 0**); poison curation lives on
  `Poisoned`(20)=171 not `Venom`(70)=2 and `GrantImmunityWith[20]={70}` is one-way; and "modded NPCs
  work for free" means **free zero** — an all-null row gives resistance 0, so most of the actual
  Calamity/Fargo modpack would be undifferentiated. Worst: `ImmuneToRegularBuffs` covers **The
  Destroyer, the Lunatic Cultist and all four Celestial Pillars**, so reading it would switch a
  ~60-point elemental investment **off for the whole mech→Lunar stretch and back on for Moon Lord**.
  A hardcoded id table has the same free-zero problem — the roll is the answer *because* it works for
  everything.
- **Rarity gives no resistance** — it already grants ×3 HP and ×2 defense, and the defense bump already
  raises Bleed resistance. Under DoT, TTK ∝ `3/(1-r)`; under direct damage, TTK ∝ `3` — so rarity
  resistance makes the investment fall behind *precisely* on the enemies it should shine against.
  It returns legibly via `MaxModifiers` (a Mythic rolls 5 affixes → likelier warded, visible in its name).

**Bugs found and fixed in §10's own spec:**

- **`min(0.9, def/(def+50))` had a pole AND a sign flip.** Undefined at `def=-50`; *positive* below it
  (`def=-60` → `+6.0` → clamps to **+0.90**, the squishiest enemy in the game resisting Bleed 90%).
  Negative NPC defense is real — EoC's `aiStyle 4` sets `-15`/`-30` in Expert phase 2. Now
  `def/(|def|+50)`: identical for `def ≥ 0`, monotone over all reals, pole-free.
- **Bleed pays for armour twice** — `damageDone` is already post-defense (`DefenseEffectiveness = 0.5`
  for NPCs). The first charge is universal; Bleed's resistance is an extra proportional one. Kept: that
  *is* "physical, limited by armour instead".

### Still open

- [ ] **Play-test all of it. Zero Build+Reload.** `RollMean` (0.15), `RollHalfWidth` (0.25),
      `WardResistance` (0.40) are first guesses, as are the **pre-existing** `ConversionPerTier`
      (2%/rank), `BaseDurationSeconds` (4s), `MaxInstancesPerElement` (20) and Plaguebearer's
      3%/element — **none of which have ever been tracked or tested either.**
- [ ] ⚠️ **The roll is INVISIBLE until Phase 4.** Wards announce themselves in the enemy's name; a +0.40
      fire roll does not, so it reads as Immolation randomly underperforming with no counterplay.
      `RollHalfWidth` is deliberately moderate for this reason. **Most likely source of "this feels bad".
      Consider pulling Phase 4 (the hover panel) forward before widening the roll.**
- [ ] ⚠️ **Wards are invisible on ~43% of bosses — found in review, not fixed.**
      `EnemyModifierSystem.GenerateDisplayName:115` renders at most the **first two** modifier prefixes:
      `prefix = modifiers[0]; if (count > 1) prefix = $"{modifiers[0]} {modifiers[1]}"`. Trash is fine
      (Uncommon rolls exactly 1, Rare 1–2 → a ward always shows). But a **previously-defeated boss always
      rolls 2–5** (`:59`), averaging 3.5, so a ward landing in slot 3+ is applied and never named — fire
      DoT quietly cut 40% with nothing on screen. **This is precisely where wards matter most**: ~50% of
      repeat bosses carry one, vs 1.67% of trash.
      - **This partly undercuts the argument for shipping Phase 2 before Phase 4.** "The prefix names the
        element" was the reason wards are legible without the hover panel; it holds for trash and fails
        for bosses.
      - Two fixes, neither obviously right: **name every modifier** (general, but yields "Mythic Tough
        Swift Regenerating Flamewarded Brutal Plantera"), or **float wards to the front of the prefix**
        (short, but a special case in shared naming code — the kind of bandaid `CLAUDE.md` argues against).
        A third option is to let Phase 4 carry it and accept boss wards are opaque until then.
- [ ] **`RollMean` is a live fork, not a settled number.** Mean-zero was the considered alternative: it
      changes the tuning identity by nothing (a flat 32%/element becomes a distribution *centred* on
      32%) and leaves total output to `ConversionPerTier`, which is arguably the honest dial for output.
      Positive was chosen so Phase 2 does the mitigating. **If elemental output is over-nerfed, move
      `RollMean` toward 0 and cut `ConversionPerTier` — do not fight the two against each other.**
- [ ] **`BleedArmourHalfPoint = 50` vs 150 — logged dissent.** The case for 150: at 50, Bleed is only
      62% of an average element for equal points, i.e. Rend is a trap. **But that assumes a mean-zero
      roll and a p75-defense enemy.** At the shipped mean and a median enemy (def 15) it is **~90%**,
      bought with zero variance and total ward immunity. **First lever to move if Rend under-performs.**
- [ ] **Median-defense elites reach ~50% Bleed resistance.** Mod defense inflation is ≤×3.36 (rarity
      ≤×2.0 × Tough ×1.5 × level ×1.12). It does **not** track world level, so it doesn't drift — but
      ×3.36 on a median def 15 is 50, which is the half-point. Watch it.
- [ ] **The clamps are dormant and therefore untested.** Elemental max is `0.40 + 0.40 = 0.80`;
      `MinResistance` floors at −0.10. Neither binds until Phase 3's penetration. For Bleed,
      `MaxResistance` binds only at `def ≥ 450` — Dungeon Guardian alone. **Don't mistake the clamp for
      a balancing lever.**
- [ ] ⚠️ **Phase 3: `ArmorPenetration` must NEVER feed `penetration`.** It already flows into
      `damageDone` via `HitModifiers.ArmorPenetration`, so wiring it in is the **Vengeance ×4 double-dip**
      (§1a) exactly. Penetration must be a *new, separate* elemental-penetration stat. Also undecided:
      **should penetration apply to Bleed at all?** The current shape subtracts it uniformly.
- [ ] **Mod-dealt AoE still does not convert** (pre-existing, `CLAUDE.md` §10): `ModPlayer.OnHitNPC` is
      dispatched only from `Player.StrikeNPCDirect`, so Detonate/Hellfire/Corrupted Blood never apply DoT.
- [ ] **New §7 bug found while doing this (not fixed):** the mod's rarity/Tough/level defense multipliers
      are written to `npc.defense` but never `npc.defDefense`, and several aiStyles reset
      `defense = defDefense` **every frame** (`aiStyle 11` Skeletron `NPC.cs:22154`, EoC, Golem, Prime).
      So on those enemies `ToughModifier` and the rarity defense bump **do nothing at all**. Wider than
      the elemental system — it silently eats a whole affix on every boss with those aiStyles.
