<script lang="ts">
	import type {
		ScannerDeviceDto,
		ScanOptionsDto,
		ScannerStatusDto
	} from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { pageSizeLabel } from '$lib/format';
	import { activePreset, exportPrefs, presets, presetsFor } from '$lib/presets';
	import { exportFormatLabels } from '$lib/format';
	import { settings } from '$lib/settings';
	import PaperStack from './PaperStack.svelte';
	import PresetPills from './PresetPills.svelte';
	import ScanButton from './ScanButton.svelte';
	import SettingsChips from './SettingsChips.svelte';
	import { Icon } from '$lib/icons';

	let {
		status,
		connection,
		options,
		devices = [],
		deviceName = null,
		busy = false,
		onscan,
		onsettings
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		options: ScanOptionsDto | null;
		/** Scanners the server can see, for the picker. */
		devices?: ScannerDeviceDto[];
		/** The chosen scanner's name, for the chips when there is no picker to say it. */
		deviceName?: string | null;
		busy?: boolean;
		onscan: () => void;
		onsettings: () => void;
	} = $props();

	/** The scanner the next scan will run on: this browser's pick, or the server's. */
	const device = $derived(options?.Device || '');

	/**
	 * Only once there is something to pick between: a lone "Automatic" is not a choice, and
	 * the line above already says when no scanner was found. yass.yaml has no say in it —
	 * the file supplies the default scanner, the person at the screen chooses.
	 */
	const canPickDevice = $derived(devices.length > 0);

	/** Presets follow the scanner, so a scanner-specific one is out of sight on the others. */
	const offered = $derived(presetsFor($presets, device || undefined));

	// Matched against what the scan will do, not against the stored overrides alone:
	// a browser with no overrides still matches whichever preset the server defaults to.
	const chosen = $derived(options ? activePreset(offered, options) : null);

	// The configured folder stands in when nothing is typed, the same fallback the
	// review screen and the settings sheet use.
	const destination = $derived(
		$exportPrefs.exportButton
			? $exportPrefs.directory.trim() || options?.ExportRoots?.[0] || null
			: null
	);

	const offline = $derived(connection !== 'connected');
	const ready = $derived(!offline && status?.State === ScannerState.Ready);

	/**
	 * Why the Scan button is greyed out, for its tooltip. Ready needs no headline of its
	 * own: the header pill says so, and a button that can be pressed says the rest.
	 */
	const blocked = $derived(
		offline
			? connection === 'connecting'
				? 'Connecting to the scanner service…'
				: 'Not connected to the scanner service'
			: status?.State === ScannerState.Busy
				? 'The scanner is busy'
				: status?.State === ScannerState.NoDevice
					? 'No scanner found'
					: status?.State === ScannerState.Error
						? 'The scanner reported a problem'
						: 'Still checking the scanner…'
	);

	const caption = $derived(
		[pageSizeLabel(options?.PageSize), options?.Dpi ? `${options.Dpi} dpi` : '']
			.filter(Boolean)
			.join(' · ')
	);
</script>

<section
	class="flex min-h-0 animate-fade-in flex-col items-center justify-center gap-[clamp(8px,1.4vh,18px)] overflow-y-auto rounded-[28px] px-5 py-8 shadow-stage sm:px-8 sm:py-[clamp(1rem,2.6vh,3rem)]"
	style="background: radial-gradient(120% 80% at 50% 0%, var(--color-accent-wash2) 0%, var(--color-surface) 60%)"
	style:flex="2 1 520px"
	data-testid="scan-stage"
>
	<PaperStack {caption} />

	<!-- Nothing when all is well: whatever the scanner has to say is a problem to show. -->
	{#if !ready}
		<p
			class="max-w-[420px] text-center text-[13px] font-semibold text-ink-muted"
			data-testid="scanner-trouble"
		>
			{status?.Message || blocked}
		</p>
	{/if}

	{#if canPickDevice}
		<div class="flex flex-col items-center gap-2" data-testid="device-picker">
			<span class="text-[11px] font-bold tracking-[0.08em] text-ink-faint uppercase">Scanner</span>
			<select
				class="h-[38px] max-w-[min(340px,80vw)] cursor-pointer truncate rounded-full bg-surface px-3.5 text-[13px] font-bold text-accent-deep shadow-chip outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
				value={device}
				onchange={(e) => settings.set({ ...$settings, Device: e.currentTarget.value || undefined })}
				data-testid="device-select"
			>
				<option value="">Automatic</option>
				{#each devices as scanner (scanner.Id)}
					<option value={scanner.Id}>{scanner.Name}</option>
				{/each}
				<!-- One the server has stopped listing still has to show, or the box would
				     silently read as Automatic while the scan goes somewhere else. -->
				{#if device && !devices.some((d) => d.Id === device)}
					<option value={device}>{device} (not found)</option>
				{/if}
			</select>
		</div>
	{/if}

	<PresetPills
		presets={offered}
		active={chosen?.id ?? null}
		modified={chosen === null}
		onpick={(id) => {
			const preset = offered.find((p) => p.id === id);
			if (preset) settings.set({ ...$settings, ...preset.settings });
		}}
		onsave={onsettings}
	/>

	<SettingsChips
		{options}
		{destination}
		deviceName={canPickDevice ? null : deviceName}
		format={exportFormatLabels[$exportPrefs.format]}
		onclick={onsettings}
	/>

	<button
		type="button"
		onclick={onsettings}
		class="flex h-9 items-center gap-2 rounded-full px-3 text-sm font-bold text-accent transition-colors hover:bg-accent-wash"
	>
		<Icon name="sliders" />
		Change settings
	</button>

	<ScanButton disabled={!ready || busy} {busy} reason={blocked} onclick={onscan} />
</section>
