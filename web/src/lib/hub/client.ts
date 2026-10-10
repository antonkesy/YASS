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
	/** Typed hub proxy (client → server). */
	readonly hub: IYassHub;
	readonly connection: Readable<ConnectionState>;
	readonly scanner: Readable<ScannerStatusDto | null>;
	readonly sessions: Readable<SessionMap>;
	/** sessionId → percent for exports in flight. */
	readonly exportProgress: Readable<Record<string, number>>;
	/**
	 * The server's output folder, or "" when it has none or would not say; null until the
	 * first connect has asked. Asked again on every connect, since a config reload can change it.
	 */
	readonly serverFolder: Readable<string | null>;
	/** Starts (or resumes) the connection and loads the initial state. Idempotent. */
	start(): Promise<void>;
	stop(): Promise<void>;
	/** Fetches one session from the server and puts it in the store. */
	refresh(sessionId: string): Promise<SessionDto>;
	/** Optimistic local change; the next SessionChanged/PageUpdated from the server wins. */
	updateLocal(sessionId: string, update: (session: SessionDto) => SessionDto): void;
};

/** Minimal surface of HubConnection this client needs; lets tests pass a fake. */
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

/** `record` without `key`, or the very same object when it had none, so nobody re-renders. */
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
			// An export's percentages end with it, so the next one starts from nothing rather
			// than from the 100% the last one left behind.
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
	// Both through start(), so a reload after a reconnect is retried like any other, and a
	// close while a start is already retrying joins that one instead of racing it.
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
		// Apart from the rest, and never failing the connect: without it Save to server is
		// simply not offered.
		hub.getServerFolder().then(
			(folder) => serverFolder.set(folder),
			() => serverFolder.set('')
		);
	}

	async function startWithRetry() {
		for (let attempt = 0; !stopped; attempt++) {
			try {
				// Already connected (a reconnect, or a retry of a failed load) is nothing to announce.
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
