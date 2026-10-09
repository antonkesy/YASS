import { HubConnectionState } from '@microsoft/signalr';
import type { ConnectionLike } from './client';

/**
 * What the desktop app's webview (Photino) puts on `window.external`: one string channel each
 * way to the .NET process. Nothing goes over a socket.
 */
export type DesktopExternal = {
	sendMessage(message: string): void;
	receiveMessage(callback: (message: string) => void): void;
};

type Answer = { id: number; result?: unknown; error?: string };
type Event = { event: string; args?: unknown[] };
type Handler = (...args: unknown[]) => unknown;

/** The desktop channel, when this page runs inside the desktop app; `null` in a browser. */
export function desktopExternal(): DesktopExternal | null {
	if (typeof window === 'undefined') return null;
	const external = (window as unknown as { external?: Partial<DesktopExternal> }).external;
	return typeof external?.sendMessage === 'function' &&
		typeof external.receiveMessage === 'function'
		? (external as DesktopExternal)
		: null;
}

/**
 * A `HubConnection` look-alike over the desktop bridge (SPEC §7): calls go out as
 * `{id, method, args}` and come back as `{id, result}` or `{id, error}`; server pushes arrive
 * as `{event, args}`. The channel is in-process, so it is up from the start and never drops.
 */
export function createBridgeConnection(external: DesktopExternal): ConnectionLike {
	const handlers = new Map<string, Handler[]>();
	const pending = new Map<number, { resolve(value: unknown): void; reject(error: Error): void }>();
	let nextId = 1;
	let state = HubConnectionState.Disconnected;

	external.receiveMessage((message) => {
		let parsed: Answer | Event;
		try {
			parsed = JSON.parse(message);
		} catch {
			return;
		}
		if ('event' in parsed) {
			for (const handler of handlers.get(parsed.event) ?? []) handler(...(parsed.args ?? []));
			return;
		}
		const call = pending.get(parsed.id);
		if (!call) return;
		pending.delete(parsed.id);
		if (parsed.error !== undefined) call.reject(new Error(parsed.error));
		else call.resolve(parsed.result);
	});

	const connection = {
		get state() {
			return state;
		},
		start: async () => {
			state = HubConnectionState.Connected;
		},
		stop: async () => {
			state = HubConnectionState.Disconnected;
		},
		on: (name: string, handler: Handler) => {
			handlers.set(name, [...(handlers.get(name) ?? []), handler]);
		},
		off: (name: string, handler?: Handler) => {
			handlers.set(name, handler ? (handlers.get(name) ?? []).filter((h) => h !== handler) : []);
		},
		invoke: (method: string, ...args: unknown[]) =>
			new Promise((resolve, reject) => {
				const id = nextId++;
				pending.set(id, { resolve, reject });
				external.sendMessage(JSON.stringify({ id, method, args }));
			}),
		// An in-process channel neither drops nor reconnects.
		onreconnecting: () => {},
		onreconnected: () => {},
		onclose: () => {}
	};
	return connection as unknown as ConnectionLike;
}
