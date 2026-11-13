#!/usr/bin/env node
/*
 * Re-simulates replays from their input logs on one Showdown version, for resim.py.
 *
 *     node resim.js <built pokemon-showdown checkout>       (JSON lines on stdin / stdout)
 *
 * Request:  {"id", "inputlog": "<the replay's inputlog>"}
 * Response: {"id", "ok", "error",
 *   "sides": {"p1": [line...], "p2": [line...]}   what each player's websocket receives from the
 *       battle: every update with its |split| lines resolved for that player (its own secret
 *       line, the public line of the others, empty lines dropped), and its sideupdates in order
 *       (|request| with the rqid Showdown's server adds: one counter for the battle, first
 *       request 2, as room-battle.ts does; |error|). Lines only the server adds (|init|, |j|,
 *       |t:| of the room, timer, chat, forfeit messages) are not here.
 *   "omniscient": [line...], "spectator": [line...]   the two other views of the updates
 *   "requests": {"p1": [{"line", "wait", "choice"}...]}   each |request| of that side: its index in
 *       the side's lines, whether it is a wait request, and the inputlog choice that answered it
 *       ("move knockoff terastallize", "switch 3"; null if none did)
 *   "choice_errors": [text...]   an inputlog choice the simulator refused or that answered no
 *       request: the replay does not replay on this version
 *   "ended": the battle reached |win| or |tie|}
 *
 * The input log is written line by line into the version's own BattleStream (its _writeLine,
 * the parser the server uses), and the updates are split with that version's own
 * extractChannelMessages. `>chat` lines are skipped (they only add |chat| lines), as are
 * `>version` lines.
 */
'use strict';

const path = require('path');
const readline = require('readline');

const SHOWDOWN = path.resolve(process.argv[2] || process.env.SHOWDOWN);
const streamModule = require(path.join(SHOWDOWN, 'dist/sim/battle-stream'));
const {BattleStream} = streamModule;
let extractChannelMessages = streamModule.extractChannelMessages;
if (!extractChannelMessages) {
	try {
		extractChannelMessages = require(path.join(SHOWDOWN, 'dist/sim/battle')).extractChannelMessages;
	} catch {}
}
if (!extractChannelMessages) {
	// Showdown's own, for a version that does not export it
	const splitRegex = /^\|split\|p([1234])\n(.*)\n(.*)|.+/gm;
	extractChannelMessages = (message, channelIds) => {
		const out = {'-1': [], 0: [], 1: [], 2: [], 3: [], 4: []};
		for (const [lineMatch, playerMatch, secret, shared] of message.matchAll(splitRegex)) {
			const player = playerMatch ? parseInt(playerMatch) : 0;
			for (const channel of channelIds) {
				let line = lineMatch;
				if (player) {
					line = channel === -1 || player === channel ? secret : shared;
					if (!line) continue;
				}
				out[channel].push(line);
			}
		}
		return out;
	};
}

const SIDES = ['p1', 'p2'];

function resim(inputlog) {
	const stream = new BattleStream({keepAlive: true});
	const chunks = [];
	stream.push = chunk => {
		chunks.push(chunk);
		return true;
	};
	const out = {
		ok: true, error: null, sides: {p1: [], p2: []}, omniscient: [], spectator: [],
		requests: {p1: [], p2: []}, choice_errors: [], ended: false,
	};
	const errors = {p1: [], p2: []};
	let rqid = 1;
	let consumed = 0;

	const handle = chunk => {
		const newline = chunk.indexOf('\n');
		const type = newline < 0 ? chunk : chunk.slice(0, newline);
		const data = newline < 0 ? '' : chunk.slice(newline + 1);
		if (type === 'update') {
			const channels = extractChannelMessages(data, [-1, 0, 1, 2]);
			out.omniscient.push(...channels[-1]);
			out.spectator.push(...channels[0]);
			out.sides.p1.push(...channels[1]);
			out.sides.p2.push(...channels[2]);
			if (channels[-1].some(line => line.startsWith('|win|') || line === '|tie')) out.ended = true;
		} else if (type === 'sideupdate') {
			// room-battle.ts reads the side from the first line and sends the second
			const lines = data.split('\n');
			const side = lines[0];
			let body = lines[1] || '';
			if (!out.sides[side]) return;
			if (body.startsWith('|request|')) {
				rqid++;
				const request = JSON.parse(body.slice(9));
				request.rqid = rqid;
				body = `|request|${JSON.stringify(request)}`;
				out.requests[side].push({line: out.sides[side].length, wait: !!request.wait, choice: null});
			} else if (body.startsWith('|error|')) {
				errors[side].push(body);
			}
			out.sides[side].push(body);
		} else if (type === 'error') {
			throw new Error(`simulator error: ${data.split('\n')[0]}`);
		}
	};
	const flush = () => {
		while (consumed < chunks.length) handle(chunks[consumed++]);
	};

	for (const raw of inputlog.split('\n')) {
		if (!raw.startsWith('>')) continue;
		const line = raw.slice(1);
		const space = line.indexOf(' ');
		const type = space < 0 ? line : line.slice(0, space);
		const message = space < 0 ? '' : line.slice(space + 1);
		if (type === 'chat' || type === 'version' || type === 'version-origin') continue;
		const isChoice = SIDES.includes(type) && message !== 'undo';
		let pending = null;
		if (isChoice) {
			const requests = out.requests[type];
			pending = requests.length ? requests[requests.length - 1] : null;
			if (!pending || pending.wait || pending.choice !== null) {
				out.choice_errors.push(`>${type} ${message}: no request waiting for it`);
				pending = null;
			}
		}
		const errorsBefore = isChoice ? errors[type].length : 0;
		// What BattleStream._write does with each line the server writes: parse it, then send
		// the updates and requests it produced (the server writes every input line on its own)
		stream._writeLine(type, message);
		if (stream.battle) stream.battle.sendUpdates();
		flush();
		if (isChoice) {
			if (errors[type].length > errorsBefore) {
				out.choice_errors.push(`>${type} ${message}: ${errors[type][errorsBefore]}`);
			} else if (pending) {
				pending.choice = message;
			}
		}
	}
	flush();
	return out;
}

const rl = readline.createInterface({input: process.stdin, terminal: false});
rl.on('line', text => {
	if (!text.trim()) return;
	let request;
	try {
		request = JSON.parse(text);
	} catch (err) {
		process.stdout.write(JSON.stringify({id: null, ok: false, error: `bad request: ${err.message}`}) + '\n');
		return;
	}
	let response;
	try {
		response = resim(request.inputlog || '');
	} catch (err) {
		response = {ok: false, error: String(err && err.stack || err).split('\n').slice(0, 3).join(' | ')};
	}
	response.id = request.id;
	process.stdout.write(JSON.stringify(response) + '\n');
});
