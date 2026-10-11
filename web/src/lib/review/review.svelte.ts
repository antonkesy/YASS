import { get } from 'svelte/store';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { getDesktop, getHub } from '$lib/hub/hub';
import { hubError } from '$lib/hub/errors';
import type { PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { t } from '$lib/i18n/index.svelte';
import { cameFrom } from '$lib/navigation';
import { documentMeta, exportPrefs, nameTemplate } from '$lib/presets';
import { toast } from '$lib/toast';
import { applyOptimistic, setCropEdit } from './edits';
import {
	cancelled,
	dialogName,
	exportBaseName,
	exportRequest,
	fileExtensions,
	savePicker,
	savePickerType,
	saveNatively,
	saveToBrowser,
	saveToDisk,
	type SaveTarget
} from './download';

export class Review {
	error = $state<string | null>(null);
	exporting = $state(false);
	stopping = $state(false);
	autocropping = $state(false);
	mergeBusy = $state(false);
	/**
	 * Held until the session comes back carrying it: hub calls are serialized per connection, so
	 * a rename can sit behind a page read, and Save as… must not use the stale name meanwhile.
	 */
	renamedTo = $state<string | null>(null);

	readonly #client = getHub();
	readonly #desktop = getDesktop();
	readonly #id: () => string;
	readonly #session: () => SessionDto | null;

	constructor(id: () => string, session: () => SessionDto | null) {
		this.#id = id;
		this.#session = session;

		$effect(() => {
			if (this.renamedTo !== null && (this.#session()?.Name ?? '') === this.renamedTo)
				this.renamedTo = null;
		});

		$effect(() => {
			if (this.stopping && this.#session()?.State !== SessionState.Scanning) {
				this.stopping = false;
				toast.show(t('review.scanStopped'));
			}
		});
	}

	get storedName() {
		return this.renamedTo ?? this.#session()?.Name ?? null;
	}

	async #attempt(call: () => Promise<unknown>) {
		this.error = null;
		try {
			await call();
		} catch (e) {
			this.error = hubError(e);
		}
	}

	async send(edit: PageEdit) {
		const id = this.#id();
		if (!this.#session()) return;
		this.error = null;
		this.#client.updateLocal(id, (s) => applyOptimistic(s, edit));
		try {
			await this.#client.hub.applyEdits(id, [edit]);
		} catch (e) {
			this.error = hubError(e);
			await this.#client.refresh(id);
		}
	}

	async discard() {
		const id = this.#id();
		try {
			await this.#client.hub.deleteSession(id);
			cameFrom.leaving(id);
			await goto(resolve('/'));
			toast.show(t('review.scanDiscarded'));
		} catch (e) {
			this.error = hubError(e);
		}
	}

	async rename(name: string) {
		this.renamedTo = name.trim();
		try {
			await this.#client.hub.renameSession(this.#id(), name);
		} catch (e) {
			this.renamedTo = null;
			this.error = hubError(e);
		}
	}

	#request(of: SessionDto, toServer: boolean) {
		return exportRequest(of, get(exportPrefs), get(documentMeta), {
			name: this.storedName,
			toServer
		});
	}

	// Picks the file first, while the click is still a user gesture: the export takes too long
	// for the browser to allow a dialog afterwards.
	async saveAs() {
		const session = this.#session();
		if (!session) return;
		const prefs = get(exportPrefs);
		const picker = savePicker();
		let target: SaveTarget | null = null;
		const named = { ...session, Name: this.storedName ?? undefined };
		const base = exportBaseName(named, nameTemplate(prefs));
		const suggested = dialogName(named, prefs);
		const extension = fileExtensions[prefs.format];

		if (this.#desktop) {
			await this.#saveAsOnDesktop(session, suggested, base, extension);
			return;
		}

		if (picker) {
			try {
				target = {
					file: await picker.showSaveFilePicker({
						suggestedName: suggested ? suggested + extension : undefined,
						types: [savePickerType(prefs.format)],
						excludeAcceptAllOption: true
					})
				};
			} catch (e) {
				if (cancelled(e)) return;
				// The dialog exists but would not open: the plain download still gets the file out.
				target = null;
			}
		}

		this.error = null;
		this.exporting = true;
		try {
			const result = await this.#client.hub.export(this.#id(), this.#request(session, false));
			const count = target
				? await saveToDisk(result, target, base, extension)
				: saveToBrowser(result);
			toast.show(t(target ? 'review.savedFiles' : 'review.downloadedFiles', { n: count }));
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.exporting = false;
		}
	}

	async #saveAsOnDesktop(session: SessionDto, suggested: string, base: string, extension: string) {
		const desktop = this.#desktop;
		if (!desktop) return;
		this.error = null;
		// Busy from the dialog on, so a second press cannot open another native dialog over it.
		this.exporting = true;
		try {
			const file = await desktop.pickSavePath(suggested, extension);
			if (!file) return;
			const result = await this.#client.hub.export(this.#id(), this.#request(session, false));
			const saved = await saveNatively(result, desktop, { file }, base, extension);
			toast.show(t('review.savedFiles', { n: saved.length }));
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.exporting = false;
		}
	}

	async saveToServer() {
		const session = this.#session();
		if (!session) return;
		this.error = null;
		this.exporting = true;
		try {
			const result = await this.#client.hub.export(this.#id(), this.#request(session, true));
			const saved = result.Documents.map((d) => d.SavedTo).filter((p): p is string => !!p);
			const file = saved[0]?.split(/[/\\]/).pop() ?? '';
			const desktop = this.#desktop !== null;
			toast.show(
				saved.length === 1
					? t(desktop ? 'review.savedToFolderAs' : 'review.savedToServerAs', { file })
					: t(desktop ? 'review.savedFilesToFolder' : 'review.savedFilesToServer', {
							n: saved.length
						})
			);
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.exporting = false;
		}
	}

	async stop() {
		this.error = null;
		this.stopping = true;
		try {
			await this.#client.hub.stopScan(this.#id());
		} catch (e) {
			this.error = hubError(e);
			this.stopping = false;
		}
	}

	scanMore() {
		return this.#attempt(() => this.#client.hub.scanMore(this.#id()));
	}

	rescan(index: number) {
		return this.#attempt(() => this.#client.hub.rescanPage(this.#id(), index));
	}

	async merge(sourceId: string) {
		this.mergeBusy = true;
		this.error = null;
		try {
			const before = this.#session()?.Pages.length ?? 0;
			const after = await this.#client.hub.mergeSession(this.#id(), sourceId);
			toast.show(t('review.pagesAdded', { n: after.Pages.length - before }));
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.mergeBusy = false;
		}
	}

	async autoCrop(targets: ReadonlySet<number>) {
		const session = this.#session();
		if (!session || this.autocropping) return;
		const before = session.Pages.filter((p) => targets.has(p.Index)).map((p) => ({
			index: p.Index,
			crop: p.Crop
		}));
		this.autocropping = true;
		this.error = null;
		try {
			const count = await this.#client.hub.autoCrop(
				this.#id(),
				before.map((p) => p.index)
			);
			if (count === 0) toast.show(t('review.noBorders'));
			else
				toast.show(t('review.croppedPages', { n: count }), () => {
					// eslint-disable-next-line svelte/prefer-svelte-reactivity
					for (const p of before) this.send(setCropEdit(p.crop, new Set([p.index])));
				});
		} catch (e) {
			this.error = hubError(e);
		} finally {
			this.autocropping = false;
		}
	}
}
