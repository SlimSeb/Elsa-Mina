#!/usr/bin/env node
/*
 * One-turn simulations for minipoke's search (search.py), on Showdown's own simulator.
 *
 *     node search_sim.js <pokemon-showdown clone>       (JSON lines on stdin / stdout)
 *
 * Request: {"id", "position", "ours": [choice...], "determinizations": D, "samples": S, "seed"}
 *   position: what our side knows (search.position_of): both sides' Pokémon (ours whole,
 *     theirs as revealed), side conditions, field, turn.
 *   ours: our candidate actions, {"kind": "move", "move": id, "tera": bool} or
 *     {"kind": "switch", "name": name}.
 * Response: {"id", "determinizations": [{"theirs": [label...], "leaves": [[[leaf...] per
 *   their action] per our action]}]}, a leaf being {"lines": [...], "winner": side or null}
 *   or null when Showdown refused one of the two choices. "lines" are the simulated turn
 *   as our side sees it: our |split| lines, the public ones of the others.
 *
 * A determinization is one guess of what we don't know: each revealed opponent Pokémon gets
 * a set from Showdown's gen 9 random battle generator (the one closest to what it revealed:
 * its revealed moves first, then the set's, its revealed ability, item and tera type), and
 * its unrevealed Pokémon are random battle sets of random species, never switched to (they
 * only keep the battle from ending early). The position is then written onto a fresh battle
 * (HP, status, boosts, PP, items, tera, a few volatiles, side conditions, weather, terrain)
 * and each pair of actions is played from that state with its own random seed.
 */
'use strict';

const path = require('path');
const readline = require('readline');

const SHOWDOWN = process.argv[2] || process.env.SHOWDOWN;
const {Battle, Dex, Teams, toID} = require(path.join(SHOWDOWN, 'dist/sim'));
const {State} = require(path.join(SHOWDOWN, 'dist/sim/state'));

const FORMAT = 'gen9randombattle';
const DEX = Dex.forFormat(FORMAT);
const SET_ATTEMPTS = 12;
const DEFAULT_TURNS = {weather: 5, terrain: 5, pseudo: 5, reflect: 5, lightscreen: 5, auroraveil: 5,
	tailwind: 4, safeguard: 5, mist: 5};

function seedOf(n) {
	return 'sodium,' + (n >>> 0).toString(16).padStart(32, '0');
}

/** The species a random battle set exists for: itself, its battle-only forme's base, or its base species. */
function setsSpecies(generator, speciesId) {
	const species = DEX.species.get(speciesId);
	const candidates = [species.id];
	if (species.battleOnly) candidates.push(...[].concat(species.battleOnly).map(toID));
	if (species.baseSpecies) candidates.push(toID(species.baseSpecies));
	return candidates.find(id => generator.randomSets[id]) || null;
}

function randomSet(generator, speciesId) {
	try {
		return generator.randomSet(speciesId, {}, false, false);
	} catch {
		return null;
	}
}

/** A full set for an opponent Pokémon, consistent with what it revealed. */
function guessSet(generator, mon) {
	const base = setsSpecies(generator, mon.species);
	const revealed = mon.moves.map(m => m.id);
	let best = null;
	let bestScore = -Infinity;
	for (let attempt = 0; base && attempt < SET_ATTEMPTS; attempt++) {
		const set = randomSet(generator, base);
		if (!set) break;
		const ids = set.moves.map(toID);
		let score = revealed.filter(id => ids.includes(id)).length;
		if (mon.ability && toID(set.ability) !== mon.ability) score -= 0.5;
		if (mon.teraType && toID(set.teraType) !== toID(mon.teraType)) score -= 0.25;
		if (score > bestScore) {
			best = set;
			bestScore = score;
		}
		if (score === revealed.length) break;
	}
	const moves = revealed.slice(0, 4);
	for (const move of best ? best.moves : []) {
		if (moves.length >= 4) break;
		if (!moves.includes(toID(move))) moves.push(toID(move));
	}
	return {
		name: mon.name,
		species: toID(best ? best.species : mon.species),
		level: mon.level,
		gender: mon.gender || (best && best.gender) || '',
		moves: moves.length ? moves : ['struggle'],
		ability: mon.ability || (best && best.ability) || 'No Ability',
		item: mon.item !== null && mon.item !== undefined ? mon.item : (best ? best.item : ''),
		teraType: mon.teraType || (best && best.teraType) || DEX.species.get(mon.species).types[0],
		evs: best ? best.evs : {hp: 85, atk: 85, def: 85, spa: 85, spd: 85, spe: 85},
		ivs: best ? best.ivs : {hp: 31, atk: 31, def: 31, spa: 31, spd: 31, spe: 31},
	};
}

/** Random battle sets of random species for the opponent's unrevealed Pokémon. */
function fillers(generator, count, taken) {
	const species = Object.keys(generator.randomSets).filter(id => !taken.has(id));
	const out = [];
	for (let i = 0; i < count && species.length; i++) {
		const id = species.splice(generator.random(species.length), 1)[0];
		const set = randomSet(generator, id);
		if (set) out.push({...set, species: toID(set.species), name: `Unrevealed ${i + 1}`});
	}
	return out;
}

function ourSet(mon) {
	return {
		name: mon.name, species: toID(mon.species), level: mon.level, gender: mon.gender || '',
		moves: mon.moves.map(m => m.id), ability: mon.ability || 'No Ability', item: mon.item || '',
		teraType: mon.teraType || DEX.species.get(mon.species).types[0],
		evs: {hp: 85, atk: 85, def: 85, spa: 85, spd: 85, spe: 85},
		ivs: {hp: 31, atk: 31, def: 31, spa: 31, spd: 31, spe: 31},
	};
}

// Sets name their species by id: Showdown renames a Pokémon whose name is its species
// ("Rotom-Wash") to its base species ("Rotom"), and our lines must keep the battle's names.

/** Actives first: a random battle has no team preview, its leads are the first Pokémon. */
function ordered(mons) {
	return [...mons.filter(m => m.active), ...mons.filter(m => !m.active)];
}

function effectState(battle, data) {
	return battle.initEffectState(data);
}

/** Writes what we know of one Pokémon onto the simulated one. */
function applyPokemon(battle, p, mon) {
	if (mon.forme && toID(p.species.name) !== mon.forme) {
		try {
			p.formeChange(mon.forme, null, true);
		} catch {
			// an unknown forme keeps the set's species
		}
	}
	if (mon.stats) {
		p.storedStats = {...p.storedStats, ...mon.stats};
	}
	if (mon.maxhp) {
		p.maxhp = p.baseMaxhp = mon.maxhp;
	}
	const hp = mon.hp !== undefined && mon.hp !== null ? mon.hp : Math.round(mon.hpFraction * p.maxhp);
	p.hp = Math.max(0, Math.min(p.maxhp, hp));
	if (mon.fainted || p.hp <= 0) {
		p.hp = 0;
		p.fainted = true;
		p.status = 'fnt';
		return;
	}
	if (p.hp === 0 && !mon.fainted) p.hp = 1;
	p.status = mon.status || '';
	p.statusState = effectState(battle, {id: p.status, target: p});
	if (p.status === 'slp') p.statusState.startTime = p.statusState.time = mon.sleepTurns || 2;
	if (p.status === 'tox') p.statusState.stage = mon.toxicStage || 1;
	for (const [stat, value] of Object.entries(mon.boosts || {})) p.boosts[stat] = value;
	for (const slot of p.moveSlots) {
		const known = (mon.moves || []).find(m => m.id === slot.id);
		if (known && known.pp !== undefined && known.pp !== null) slot.pp = Math.max(0, Math.min(slot.maxpp, known.pp));
	}
	if (mon.item !== null && mon.item !== undefined) p.item = mon.item;
	if (mon.terastallized) {
		p.teraType = mon.terastallized;
		battle.actions.terastallize(p);
	}
	if (p.isActive) {
		p.activeTurns = p.activeMoveActions = mon.activeTurns || 0;
		for (const volatile of mon.volatiles || []) addVolatile(battle, p, volatile);
	}
}

function addVolatile(battle, p, volatile) {
	const id = volatile.id;
	const data = {id, target: p};
	if (id === 'substitute') data.hp = Math.floor(p.maxhp / 4);
	else if (id === 'leechseed') data.sourceSlot = p.side.foe.active[0] ? p.side.foe.active[0].getSlot() : 'p1a';
	else if (id === 'confusion') data.time = 2;
	else if (id === 'taunt' || id === 'yawn') data.duration = id === 'yawn' ? 2 : 3;
	else if (id === 'choicelock') data.move = volatile.move;
	else if (!['focusenergy', 'saltcure'].includes(id)) return;
	p.volatiles[id] = effectState(battle, data);
}

function applySide(battle, side, desc) {
	for (const mon of desc.pokemon) {
		const p = side.pokemon.find(candidate => candidate.name === mon.name);
		if (p) applyPokemon(battle, p, mon);
	}
	side.pokemonLeft = side.pokemon.filter(p => !p.fainted).length;
	if (desc.teraUsed) for (const p of side.pokemon) p.canTerastallize = null;
	side.sideConditions = {};
	for (const [id, value] of Object.entries(desc.conditions || {})) {
		const state = effectState(battle, {id, target: side});
		if (id === 'spikes' || id === 'toxicspikes') state.layers = value;
		else if (DEFAULT_TURNS[id]) state.duration = Math.max(1, value);
		side.sideConditions[id] = state;
	}
}

function applyField(battle, field) {
	const f = battle.field;
	f.weather = field.weather || '';
	f.weatherState = effectState(battle, {id: f.weather, duration: field.weatherTurns || undefined});
	f.terrain = field.terrain || '';
	f.terrainState = effectState(battle, {id: f.terrain, duration: field.terrainTurns || undefined});
	f.pseudoWeather = {};
	for (const [id, turns] of Object.entries(field.pseudo || {})) {
		f.pseudoWeather[id] = effectState(battle, {id, duration: turns || undefined});
	}
}

/** One determinization of the position, ready to play turns from. */
function build(position, seed) {
	const generator = Teams.getGenerator(FORMAT, seedOf(seed));
	const us = position.us;
	const them = us === 'p1' ? 'p2' : 'p1';
	const ours = position.sides[us];
	const theirs = position.sides[them];
	const guessed = ordered(theirs.pokemon).map(mon => guessSet(generator, mon));
	const taken = new Set(guessed.map(set => toID(set.species)));
	const teams = {
		[us]: ordered(ours.pokemon).map(ourSet),
		[them]: [...guessed, ...fillers(generator, Math.max(0, theirs.size - guessed.length), taken)],
	};
	const battle = new Battle({formatid: FORMAT, seed: seedOf(seed + 1)});
	battle.setPlayer('p1', {name: 'p1', team: teams.p1});
	battle.setPlayer('p2', {name: 'p2', team: teams.p2});
	battle.turn = position.turn;
	applySide(battle, battle[us], ours);
	applySide(battle, battle[them], theirs);
	applyField(battle, position.field || {});
	battle.makeRequest('move');
	return battle;
}

function ourChoice(battle, side, action) {
	if (action.kind === 'switch') {
		const index = battle[side].pokemon.findIndex(p => p.name === action.name);
		return index < 0 ? null : `switch ${index + 1}`;
	}
	return `move ${action.move}${action.tera ? ' terastallize' : ''}`;
}

/** The opponent's actions: its active's usable moves (and tera), its revealed Pokémon to switch to. */
function theirChoices(battle, side, revealed) {
	const request = battle[side].activeRequest;
	const out = [];
	if (!request || !request.active) return out;
	const active = request.active[0];
	active.moves.forEach((move, i) => {
		if (move.disabled) return;
		out.push({choice: `move ${i + 1}`, label: move.id});
		if (active.canTerastallize) out.push({choice: `move ${i + 1} terastallize`, label: `${move.id}+tera`});
	});
	if (!active.trapped) {
		battle[side].pokemon.forEach((p, i) => {
			if (!p.isActive && !p.fainted && revealed.has(p.name)) out.push({choice: `switch ${i + 1}`, label: p.name});
		});
	}
	return out;
}

/** The log as `side` sees it: its own |split| lines, the public ones of the others. */
function view(log, side) {
	const out = [];
	for (let i = 0; i < log.length; i++) {
		const line = log[i];
		if (line.startsWith('|split|')) {
			out.push(line.slice(7) === side ? log[i + 1] : log[i + 2]);
			i += 2;
		} else if (line !== '|' && !line.startsWith('|t:|')) {
			out.push(line);
		}
	}
	return out;
}

function play(snapshot, us, them, mine, theirs, seed) {
	const battle = State.deserializeBattle(snapshot);
	battle.log = [];
	battle.sentLogPos = 0;
	battle.resetRNG(seedOf(seed));
	if (!battle.choose(us, mine) || !battle.choose(them, theirs)) return null;
	// Mid-turn switches (U-turn, Eject Button...) take the first Pokémon available
	for (let guard = 0; !battle.ended && battle.midTurn && battle.requestState === 'switch' && guard < 4; guard++) {
		for (const side of battle.sides) {
			if (side.activeRequest && !side.isChoiceDone()) side.autoChoose();
		}
		if (!battle.allChoicesDone()) break;
		battle.commitChoices();
	}
	return {lines: view(battle.log, us), winner: battle.ended ? (battle.winner === us ? us : battle.winner ? them : 'tie') : null};
}

function simulate(request) {
	const position = request.position;
	const us = position.us;
	const them = us === 'p1' ? 'p2' : 'p1';
	const revealed = new Set(position.sides[them].pokemon.map(mon => mon.name));
	const determinizations = [];
	for (let d = 0; d < request.determinizations; d++) {
		const seed = (request.seed + 7919 * d) >>> 0;
		const battle = build(position, seed);
		const snapshot = JSON.stringify(State.serializeBattle(battle));
		const theirs = theirChoices(battle, them, revealed);
		const leaves = request.ours.map((action, a) => {
			const mine = ourChoice(battle, us, action);
			return theirs.map((their, b) => {
				if (!mine) return Array(request.samples).fill(null);
				const out = [];
				for (let s = 0; s < request.samples; s++) {
					try {
						out.push(play(snapshot, us, them, mine, their.choice, seed * 31 + a * 1009 + b * 97 + s));
					} catch (error) {
						out.push(null);
					}
				}
				return out;
			});
		});
		determinizations.push({theirs: theirs.map(t => t.label), leaves});
	}
	return {id: request.id, determinizations};
}

const input = readline.createInterface({input: process.stdin});
input.on('line', line => {
	let request;
	try {
		request = JSON.parse(line);
		process.stdout.write(JSON.stringify(simulate(request)) + '\n');
	} catch (error) {
		process.stdout.write(JSON.stringify({id: request ? request.id : null, error: String(error && error.stack || error)}) + '\n');
	}
});
