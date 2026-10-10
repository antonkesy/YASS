import type { HubConnection } from '@microsoft/signalr';
import { HubConnectionState } from '@microsoft/signalr';
import { get, writable, type Readable } from 'svelte/store';
import { getHubProxyFactory, getReceiverRegister } from './generated/TypedSignalR.Client';
import type { IYassHub } from './generated/TypedSignalR.Client/Yass.Core.Hub';
import type { ScannerStatusDto, SessionDto } from './generated/Yass.Core.Hub';
import { SessionState } from './generated/Yass.Core.Domain';
import {
	applyPageAdded,
	applyPageUpdated,
	applySessionChanged,
	replaceAll,
	type SessionMap
} from './state';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export type YassHubClient = {
	readonly hub: IYassHub;
	readonly connection: Readable<ConnectionState>;
	readonly scanner: Readable<ScannerStatusDto | null>;
	readonly sessions: Readable<SessionMap>;
	readonly exportProgress: Readable<Record<string, number>>;
	/** "" when the server has none; null until asked. Re-asked on every connect (config reload). */
	readonly serverFolder: Readable<string | null>;
	start(): Promise<void>;
	stop(): Promise<void>;
	refresh(sessionId: string): Promise<SessionDto>;
	updateLocal(sessionId: string, update: (session: SessionDto) => SessionDto): void;
};

export type ConnectionLike = Pick<
	HubConnection,
	| 'start'
	| 'stop'
	| 'on'
	| 'off'
	| 'invoke'
	| 'onreconnecting'
	| 'onreconnected'
	| 'onclose'
	| 'state'
>;

const RETRY_MS = [500, 1000, 2000, 5000, 10_000];

function without<T>(record: Record<string, T>, key: string): Record<string, T> {
	if (!(key in record)) return record;
	const rest = { ...record };
	delete rest[key];
	return rest;
}

export function createHubClient(connection: ConnectionLike): YassHubClient {
	const connectionState = writable<ConnectionState>('disconnected');
	const scanner = writable<ScannerStatusDto | null>(null);
	const sessions = writable<SessionMap>(new Map());
	const exportProgress = writable<Record<string, number>>({});
	const serverFolder = writable<string | null>(null);

	const hub = getHubProxyFactory('IYassHub').createHubProxy(connection as HubConnection);

	getReceiverRegister('IYassClient').register(connection as HubConnection, {
		scannerStatusChanged: async (status) => scanner.set(status),
		sessionChanged: async (session) => {
			sessions.update((m) => applySessionChanged(m, session));

			if (session.State !== SessionState.Exporting)
				exportProgress.update((p) => without(p, session.Id));
		},
		pageAdded: async (id, page) => {
			if (!get(sessions).has(id)) {
				await refresh(id).catch(() => undefined);
			}
			sessions.update((m) => applyPageAdded(m, id, page));
		},
		pageUpdated: async (id, page) => sessions.update((m) => applyPageUpdated(m, id, page)),
		exportProgress: async (id, percent) => exportProgress.update((p) => ({ ...p, [id]: percent }))
	});

	connection.onreconnecting(() => connectionState.set('reconnecting'));
	// Both go through start(), so a close during a retrying start joins it instead of racing it.
	connection.onreconnected(() => {
		connectionState.set('connected');
		if (!stopped) void start();
	});
	connection.onclose(() => {
		connectionState.set('disconnected');
		if (!stopped) void start();
	});

	let stopped = false;
	let starting: Promise<void> | null = null;

	async function loadInitial() {
		const [status, list] = await Promise.all([hub.getScannerStatus(), hub.listSessions()]);
		scanner.set(status);
		sessions.set(replaceAll(list));
		// Never fails the connect: without it, Save to server is simply not offered.
		hub.getServerFolder().then(
			(folder) => serverFolder.set(folder),
			() => serverFolder.set('')
		);
	}

	async function startWithRetry() {
		for (let attempt = 0; !stopped; attempt++) {
			try {
				if (connection.state !== HubConnectionState.Connected) {
					connectionState.set(attempt === 0 ? 'connecting' : 'reconnecting');
					await connection.start();
				}
				connectionState.set('connected');
				await loadInitial();
				return;
			} catch {
				connectionState.set('disconnected');
				await new Promise((r) => setTimeout(r, RETRY_MS[Math.min(attempt, RETRY_MS.length - 1)]));
			}
		}
	}

	async function start() {
		stopped = false;
		starting ??= startWithRetry().finally(() => (starting = null));
		return starting;
	}

	async function stop() {
		stopped = true;
		await connection.stop();
		connectionState.set('disconnected');
	}

	async function refresh(sessionId: string) {
		const session = await hub.getSession(sessionId);
		sessions.update((m) => applySessionChanged(m, session));
		return session;
	}

	function updateLocal(sessionId: string, update: (session: SessionDto) => SessionDto) {
		sessions.update((m) => {
			const current = m.get(sessionId);
			return current ? new Map(m).set(sessionId, update(current)) : m;
		});
	}

	return {
		hub,
		connection: { subscribe: connectionState.subscribe },
		scanner: { subscribe: scanner.subscribe },
		sessions: { subscribe: sessions.subscribe },
		exportProgress: { subscribe: exportProgress.subscribe },
		serverFolder: { subscribe: serverFolder.subscribe },
		start,
		stop,
		refresh,
		updateLocal
	};
}
