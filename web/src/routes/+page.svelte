<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { toast } from '$lib/toast';
	import { t } from '$lib/i18n/index.svelte';
	import { cameFrom } from '$lib/navigation';
	import { getDesktop, getHub, sortedSessions } from '$lib/hub/hub';
	import { devicesStale } from '$lib/hub/state';
	import type {
		ScannerDeviceDto,
		ScannerStatusDto,
		ScanOptionsDto
	} from '$lib/hub/generated/Yass.Core.Hub';
	import Alert from '$lib/components/Alert.svelte';
	import ScanStage from '$lib/components/ScanStage.svelte';
	import PastScansPanel from '$lib/components/PastScansPanel.svelte';
	import { settings } from '$lib/settings';
	import {
		exportBaseName,
		exportRequest,
		fileExtensions,
		saveNatively,
		saveToBrowser
	} from '$lib/review/download';
	import {
		documentMeta,
		exportPrefs,
		historyPrefs,
		nameTemplate,
		pruneDocumentMeta,
		scannerPrefs
	} from '$lib/presets';
	import { pickable, remember, scannerName } from '$lib/scanners';
	import { screenTransition } from '$lib/motion';
	import { expiredSessions } from '$lib/sessions/retention';
	import { untrack } from 'svelte';
	import { SvelteSet } from 'svelte/reactivity';

	const client = getHub();
	const desktop = getDesktop();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;

	let starting = $state(false);
	let working = $state(false);
	let error = $state<string | null>(null);
	let effective = $state<ScanOptionsDto | null>(null);
	let devices = $state<ScannerDeviceDto[]>([]);

	$effect(() => {
		if ($connection === 'connected' && !effective) {
			client.hub
				.getEffectiveOptions()
				.then((o) => (effective = o))
				.catch(() => undefined);
		}
	});

	// Asked again on every reconnect and status change, so a scanner enumerated late still shows up.
	let seenStatus: ScannerStatusDto | null = null;
	let lastConnected = false;
	$effect(() => {
		const connected = $connection === 'connected';
		const status = $scanner;
		const reconnected = connected && !lastConnected;
		const changed = devicesStale(seenStatus, status);
		lastConnected = connected;
		seenStatus = status;
		if (!connected || !(reconnected || changed)) return;
		client.hub
			.listDevices()
			.then((d) => {
				devices = d;
				// Remembered, so Settings can still list a scanner once it is unplugged.
				scannerPrefs.set(remember($scannerPrefs, d));
			})
			.catch(() => undefined);
	});

	const recent = $derived(sortedSessions($sessions));

	$effect(() => {
		if ($connection !== 'connected' || $sessions.size === 0) return;
		const existing = new Set($sessions.keys());
		const all = untrack(() => $documentMeta);
		if (Object.keys(all).some((id) => !existing.has(id)))
			documentMeta.set(pruneDocumentMeta(all, existing));
	});

	// Read once on mount.
	const landed = cameFrom.take();
	const cameBack = landed !== null;
	// A view transition already slides the screen in; its own entrance would be a second slide.
	const entrance = screenTransition.active
		? ''
		: cameBack
			? 'animate-slide-from-left'
			: 'animate-fade-in';

	const planned = $derived(effective ? { ...effective, ...$settings } : null);

	const offered = $derived(pickable($scannerPrefs, devices, planned?.Device));

	const deviceName = $derived(
		planned?.Device
			? scannerName(
					$scannerPrefs,
					planned.Device,
					devices.find((d) => d.Id === planned.Device)?.Name
				)
			: null
	);

	async function scan() {
		starting = true;
		error = null;
		try {
			const session = await client.hub.startScan($settings);
			await goto(resolve('/session/[id]', { id: session.Id }));
		} catch (e) {
			error = hubError(e);
		} finally {
			starting = false;
		}
	}

	async function downloadAll(ids: string[]) {
		error = null;
		try {
			// The desktop app asks once for a folder and saves every scan into it instead.
			const folder = desktop ? await desktop.pickFolder() : null;
			if (desktop && !folder) return;
			working = true;
			let files = 0;
			for (const id of ids) {
				const session = $sessions.get(id);
				if (!session) continue;
				const result = await client.hub.export(
					id,
					exportRequest(session, $exportPrefs, $documentMeta)
				);
				if (desktop && folder) {
					const base = exportBaseName(session, nameTemplate($exportPrefs));
					const extension = fileExtensions[$exportPrefs.format];
					files += (await saveNatively(result, desktop, { folder }, base, extension)).length;
				} else {
					files += saveToBrowser(result);
				}
			}
			toast.show(files === 1 ? t('home.downloaded') : t('home.downloadedFiles', { n: files }));
		} catch (e) {
			error = hubError(e);
		} finally {
			working = false;
		}
	}

	// Also once a minute, so scans age out while the page is left open; a failed delete retries on the next sweep.
	const sweeping = new SvelteSet<string>();

	async function sweep() {
		if ($connection !== 'connected') return;
		for (const s of expiredSessions(sortedSessions($sessions), $historyPrefs)) {
			if (sweeping.has(s.Id)) continue;
			sweeping.add(s.Id);
			try {
				await client.hub.deleteSession(s.Id);
			} catch {
				// gone already, or the server said no: the next sweep will see which
			} finally {
				sweeping.delete(s.Id);
			}
		}
	}

	$effect(() => {
		void $sessions;
		void $historyPrefs;
		void $connection;
		void untrack(sweep);
	});

	$effect(() => {
		const timer = setInterval(() => void sweep(), 60_000);
		return () => clearInterval(timer);
	});

	async function deleteAll(ids: string[]) {
		error = null;
		working = true;
		try {
			for (const id of ids) await client.hub.deleteSession(id);
			toast.show(t('home.scansDeleted', { n: ids.length }));
		} catch (e) {
			error = hubError(e);
		} finally {
			working = false;
		}
	}

	async function clearAll() {
		error = null;
		try {
			const gone = await client.hub.clearSessions();
			toast.show(t('home.scansDeleted', { n: gone }));
		} catch (e) {
			error = hubError(e);
		}
	}

	function openSettings() {
		void goto(resolve('/settings'));
	}

	function savePreset() {
		void goto(resolve('/settings'), { state: { newPreset: true } });
	}
</script>

<svelte:head><title>YASS</title></svelte:head>

<main class="flex flex-col gap-6 px-4 pt-3 pb-7 sm:px-7 {entrance}">
	{#if error}
		<Alert>{error}</Alert>
	{/if}

	<!-- From lg the panes get exactly the height under the header and main's padding (2.5rem). -->
	<div
		class="flex flex-wrap items-stretch gap-6 lg:h-[calc(100dvh-var(--app-header-h)-2.5rem)] lg:min-h-0 lg:flex-nowrap"
	>
		<ScanStage
			status={$scanner}
			connection={$connection}
			options={planned}
			devices={offered}
			{deviceName}
			busy={starting}
			onscan={scan}
			onsettings={openSettings}
			onsavepreset={savePreset}
		/>
		<PastScansPanel
			sessions={recent}
			busy={working}
			onclear={clearAll}
			ondownload={downloadAll}
			ondelete={deleteAll}
			{landed}
		/>
	</div>
</main>
