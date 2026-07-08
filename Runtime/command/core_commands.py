from __future__ import annotations
from typing import Any, List
from Runtime.command.command_registry import Command
from Runtime.command.command_result import CommandResult
from Runtime.world.world_state import GroundItem

DIRECTIONS = {
    "north":"N", "south":"S", "east":"E", "west":"W", "up":"U", "down":"D",
    "northeast":"NE", "northwest":"NW", "southeast":"SE", "southwest":"SW",
}

class LookCommand(Command):
    name = "look"
    aliases = ["l"]
    help = "look [target] - Show room or target summary."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        if actor is None:
            return CommandResult.fail("Actor not found.")
        if parsed.arg_text:
            target = ctx.world.resolve_target(ctx.actor_id, parsed.arg_text)
            if target:
                return CommandResult.success(f"You look at {target.name}.", target=target.runtime_id, hp=target.hp, max_hp=target.max_hp)
            return CommandResult.fail("You do not see that here.")
        room_ref = actor.room_ref
        monsters = [m.name for m in ctx.world.monsters_in_room(room_ref)]
        items = [i.name for i in ctx.world.items_in_room(room_ref)]
        return CommandResult.success(f"Room {room_ref}. Monsters: {', '.join(monsters) or 'none'}. Items: {', '.join(items) or 'none'}.", room=room_ref, monsters=monsters, items=items)

class MoveCommand(Command):
    name = "move"
    aliases = list(DIRECTIONS.keys())
    help = "north/south/east/west/up/down - Move through an open exit."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        if actor is None:
            return CommandResult.fail("Actor not found.")
        direction_word = parsed.verb if parsed.verb in DIRECTIONS else (parsed.args[0].lower() if parsed.args else "")
        if direction_word not in DIRECTIONS:
            return CommandResult.fail("Move where?")
        direction = DIRECTIONS[direction_word]
        if ctx.movement_runtime is None:
            return CommandResult.fail("Movement runtime is not attached.")
        result = ctx.movement_runtime.move(actor.room_ref, direction)
        if not result.moved:
            return CommandResult.fail(f"You cannot go that way: {result.status}", status=result.status)
        actor.room_ref = result.to_room
        ctx.world.current_room = result.to_room
        ctx.world.add_event("player_moved", actor.runtime_id, message=f"{actor.name} moves {direction}.", data={"from": result.from_room, "to": result.to_room, "direction": direction})
        return CommandResult.success(f"You move {direction} to {result.to_room}.", from_room=result.from_room, to_room=result.to_room)

class InventoryCommand(Command):
    name = "inventory"
    aliases = ["i", "inv"]
    help = "inventory - Show carried items."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        names = [str(item.get("name") or item.get("id") or item) for item in actor.inventory]
        return CommandResult.success("Inventory: " + (", ".join(names) if names else "empty"), items=names)

class EquipmentCommand(Command):
    name = "equipment"
    aliases = ["eq"]
    help = "equipment - Show equipped items."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        names = {slot: str(item.get("name") or item.get("id") or item) for slot, item in actor.equipment.items()}
        return CommandResult.success("Equipment: " + (", ".join(f"{k}:{v}" for k,v in names.items()) if names else "none"), equipment=names)

class GetCommand(Command):
    name = "get"
    aliases = ["take"]
    help = "get <item> - Pick up an item."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        if not parsed.arg_text:
            return CommandResult.fail("Get what?")
        item = ctx.world.find_ground_item(actor.room_ref, parsed.arg_text)
        if item is None:
            return CommandResult.fail("That item is not here.")
        ctx.world.remove_ground_item(item)
        actor.inventory.append({"id": item.item_id, "name": item.name, **item.data})
        ctx.world.add_event("item_picked_up", actor.runtime_id, message=f"{actor.name} gets {item.name}.", data={"item_id": item.item_id})
        return CommandResult.success(f"You get {item.name}.", item=item.item_id)

class DropCommand(Command):
    name = "drop"
    help = "drop <item> - Drop an item."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        if not parsed.arg_text:
            return CommandResult.fail("Drop what?")
        wanted = parsed.arg_text.lower()
        for idx, item in enumerate(actor.inventory):
            name = str(item.get("name") or item.get("id") or "").lower()
            if name == wanted or name.startswith(wanted):
                removed = actor.inventory.pop(idx)
                ground = GroundItem(str(removed.get("id") or removed.get("name")), str(removed.get("name") or removed.get("id")), actor.room_ref, data=removed)
                ctx.world.ground_items.append(ground)
                ctx.world.add_event("item_dropped", actor.runtime_id, message=f"{actor.name} drops {ground.name}.", data={"item_id": ground.item_id})
                return CommandResult.success(f"You drop {ground.name}.", item=ground.item_id)
        return CommandResult.fail("You are not carrying that.")

class AttackCommand(Command):
    name = "attack"
    aliases = ["kill"]
    help = "attack <target> - Start or resolve an attack."
    def execute(self, ctx, parsed):
        from Runtime.combat.combat_runtime import CombatState, CombatantSnapshot, resolve_attack
        if not parsed.arg_text:
            return CommandResult.fail("Attack what?")
        actor = ctx.world.get_entity(ctx.actor_id)
        target = ctx.world.resolve_target(ctx.actor_id, parsed.arg_text)
        if target is None:
            return CommandResult.fail("You do not see that target here.")
        a = CombatantSnapshot(actor.runtime_id, actor.name, actor.entity_type, actor.level, actor.max_hp, actor.hp, actor.stats, actor.skills, actor.equipment_totals, actor.flags)
        d = CombatantSnapshot(target.runtime_id, target.name, target.entity_type, target.level, target.max_hp, target.hp, target.stats, target.skills, target.equipment_totals, target.flags)
        state = CombatState({a.runtime_id: a, d.runtime_id: d})
        attack = resolve_attack(state, a.runtime_id, d.runtime_id)
        actor.hp = a.hp; target.hp = d.hp
        ctx.world.combat_pairs[actor.runtime_id] = target.runtime_id
        ctx.world.add_event("combat_attack", actor.runtime_id, target.runtime_id, message=state.events[-1].message if state.events else "Attack resolved.", data=attack.to_jsonable())
        if not target.alive:
            ctx.world.corpses.append({"source_id": target.runtime_id, "name": target.name, "room_ref": target.room_ref, "tick": ctx.world.tick})
            ctx.world.add_event("corpse_created", target.runtime_id, message=f"Corpse of {target.name} created.")
        return CommandResult.success(state.events[-1].message if state.events else "Attack resolved.", attack=attack.to_jsonable())

class CastCommand(Command):
    name = "cast"
    help = "cast <spell> <target> - Cast a simple spell by keyword."
    def execute(self, ctx, parsed):
        from Runtime.spells.spell_runtime import SpellCastRequest, SpellRuntime
        if len(parsed.args) < 1:
            return CommandResult.fail("Cast what?")
        actor = ctx.world.get_entity(ctx.actor_id)
        target_query = " ".join(parsed.args[1:]) if len(parsed.args) > 1 else actor.name
        target = ctx.world.resolve_target(ctx.actor_id, target_query) if target_query.lower() != actor.name.lower() else actor
        if target is None:
            return CommandResult.fail("You do not see that target here.")
        spell_name = parsed.args[0]
        spell = {"id": spell_name, "name": spell_name, "mana_cost": 1}
        runtime = ctx.spell_runtime or SpellRuntime(seed=ctx.world.tick)
        result = runtime.resolve_cast(SpellCastRequest(actor.to_actor_dict(), target.to_actor_dict(), spell, seed=ctx.world.tick))
        actor.mana = max(0, actor.mana - result.cost_paid.get("mana", 0))
        target.hp = max(0, min(target.max_hp, target.hp + result.hp_delta))
        ctx.world.status_effects.extend(result.status_effects)
        for event in result.events:
            ctx.world.add_event(event.get("event_type", "spell_event"), actor.runtime_id, target.runtime_id, data=event)
        return CommandResult.success(f"Cast {spell_name}: {'success' if result.success else 'failed'}.", spell_result={"success": result.success, "hp_delta": result.hp_delta})

class SayCommand(Command):
    name = "say"
    aliases = ["'"]
    help = "say <message> - Speak in the room."
    def execute(self, ctx, parsed):
        actor = ctx.world.get_entity(ctx.actor_id)
        msg = parsed.arg_text
        ctx.world.add_event("room_say", actor.runtime_id, message=f"{actor.name} says, '{msg}'", data={"text": msg})
        return CommandResult.success(f"You say, '{msg}'")

class WhisperCommand(Command):
    name = "whisper"
    aliases = ["tell"]
    help = "whisper <target> <message> - Private local message scaffold."
    def execute(self, ctx, parsed):
        if len(parsed.args) < 2:
            return CommandResult.fail("Whisper to whom and what?")
        target = ctx.world.resolve_target(ctx.actor_id, parsed.args[0])
        if target is None:
            return CommandResult.fail("That target is not here.")
        msg = " ".join(parsed.args[1:])
        ctx.world.add_event("whisper", ctx.actor_id, target.runtime_id, message=msg)
        return CommandResult.success(f"You whisper to {target.name}.")

class WhoCommand(Command):
    name = "who"
    help = "who - List connected/runtime players."
    def execute(self, ctx, parsed):
        names = [p.name for p in ctx.world.players.values()]
        return CommandResult.success("Players: " + ", ".join(names), players=names)

class HelpCommand(Command):
    name = "help"
    aliases = ["?"]
    help = "help - Show commands."
    def execute(self, ctx, parsed):
        names = ctx.runtime_kernel.registry.command_names() if ctx.runtime_kernel else []
        return CommandResult.success("Commands: " + ", ".join(names), commands=names)

class BuyCommand(Command):
    name = "buy"
    help = "buy <item> - Economy hook placeholder."
    def execute(self, ctx, parsed):
        ctx.world.add_event("npc_service_requested", ctx.actor_id, message="Buy command routed to economy hook.", data={"item": parsed.arg_text})
        return CommandResult.success("Buy request accepted by economy hook.", item=parsed.arg_text)

class DepositCommand(Command):
    name = "deposit"
    help = "deposit <amount> - Banking hook placeholder."
    def execute(self, ctx, parsed):
        ctx.world.add_event("bank_deposit_requested", ctx.actor_id, data={"amount": parsed.arg_text})
        return CommandResult.success("Deposit request accepted by banking hook.", amount=parsed.arg_text)


def register_core_commands(registry):
    for command in [LookCommand(), MoveCommand(), InventoryCommand(), EquipmentCommand(), GetCommand(), DropCommand(), AttackCommand(), CastCommand(), SayCommand(), WhisperCommand(), WhoCommand(), HelpCommand(), BuyCommand(), DepositCommand()]:
        registry.register(command)
    return registry
