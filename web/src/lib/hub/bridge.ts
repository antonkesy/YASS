import { HubConnectionState } from '@microsoft/signalr';
import type { ConnectionLike } from './client';

/** Photino's `window.external`: one string channel each way to the .NET process. */
export type DesktopExternal = {
	sendMessage(message: string): void;
	receiveMessage(callback: (message: string) => void): void;
};

type Answer = { id: number; result?: unknown; error?: string };
type Event = { event: string; args?: unknown[] };
type Handler = (...args: unknown[]) => unknown;

/** What AndroidX WebKit's `addWebMessageListener` injects into the Android app's page. */
export type AndroidChannel = {
	postMessage(message: string): void;
	onmessage: ((event: { data: string }) => void) | null;
};

export function desktopExternal(): DesktopExternal | null {
	if (typeof window === 'undefined') return null;
	const host = window as unknown as {
		external?: Partial<DesktopExternal>;
		yassBridge?: Partial<AndroidChannel>;
	};
	const android = host.yassBridge;
	if (typeof android?.postMessage === 'function') return androidExternal(android as AndroidChannel);
	const external = host.external;
	return typeof external?.sendMessage === 'function' &&
		typeof external.receiveMessage === 'function'
		? (external as DesktopExternal)
		: null;
}

/** The Android app's channel, shaped like Photino's: the same bridge protocol runs over both. */
function androidExternal(channel: AndroidChannel): DesktopExternal {
	return {
		sendMessage: (message) => channel.postMessage(message),
		receiveMessage: (callback) => {
			channel.onmessage = (event) => callback(event.data);
		}
	};
}

export function isDesktop(): boolean {
	return desktopExternal() !== null;
}

export type DesktopHost = {
	/** `suggestedName` without the extension; empty opens the dialog with no name filled in. */
	pickSavePath(suggestedName: string, extension: string): Promise<string | null>;
	pickFolder(): Promise<string | null>;
	/** `overwrite` when the user picked the file; otherwise a taken name gets -2, -3…. */
	saveExport(downloadPath: string, target: string, overwrite: boolean): Promise<string>;
	openUrl(url: string): Promise<void>;
};

export function createDesktopHost(connection: Pick<ConnectionLike, 'invoke'>): DesktopHost {
	return {
		pickSavePath: (name, extension) =>
			connection.invoke<string | null>('PickSavePath', name, extension),
		pickFolder: () => connection.invoke<string | null>('PickFolder'),
		saveExport: (path, target, overwrite) =>
			connection.invoke<string>('SaveExport', path, target, overwrite),
		openUrl: (url) => connection.invoke<void>('OpenUrl', url)
	};
}

/**
 * `HubConnection` look-alike over the desktop bridge (SPEC §7): `{id, method, args}` out,
 * `{id, result|error}` or `{event, args}` back. In-process, so never drops.
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
		onreconnecting: () => {},
		onreconnected: () => {},
		onclose: () => {}
	};
	return connection as unknown as ConnectionLike;
}
