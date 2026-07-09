#!/usr/bin/env python3
"""Deterministic probes for CodeUpdate-0070 candidate formula behaviors.
These probes validate arithmetic shape only; they do not prove Classic MajorMUD formulas.
"""
import math, json

def spawn_chance_percent(mobs:int, max_regen:int)->float:
    if mobs <= 0 or max_regen < 0:
        raise ValueError("mobs must be >0 and max_regen must be >=0")
    return round(1-(1-(1/mobs))**max_regen,2)*100

def damage_per_round(attack_damage:float, spell_value:float)->float:
    return attack_damage + spell_value

if __name__ == "__main__":
    print(json.dumps({
        "spawn_examples":[{"mobs":m,"max_regen":r,"modeled_percent":spawn_chance_percent(m,r)} for m in [1,2,3,5,10] for r in [1,2,5,10]],
        "damage_per_round_examples":[damage_per_round(10,5), damage_per_round(0,12)],
        "classic_verified": False
    }, indent=2))
