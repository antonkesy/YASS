<script lang="ts">
	import type { ScanOptionsDto, ScannerStatusDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { pageSizeLabel } from '$lib/format';
	import { activePreset, exportPrefs, presets } from '$lib/presets';
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
		busy = false,
		onscan,
		onsettings
	}: {
		status: ScannerStatusDto | null;
		connection: ConnectionState;
		options: ScanOptionsDto | null;
		busy?: boolean;
		onscan: () => void;
		onsettings: () => void;
	} = $props();

	// Matched against what the scan will do, not against the stored overrides alone:
	// a browser with no overrides still matches whichever preset the server defaults to.
	const chosen = $derived(options ? activePreset($presets, options) : null);

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
	<PaperStack {caption} sweeping={busy} />

	<!-- Nothing when all is well: whatever the scanner has to say is a problem to show. -->
	{#if !ready}
		<p
			class="max-w-[420px] text-center text-[13px] font-semibold text-ink-muted"
			data-testid="scanner-trouble"
		>
			{status?.Message || blocked}
		</p>
	{/if}

	<PresetPills
		presets={$presets}
		active={chosen?.id ?? null}
		modified={chosen === null}
		onpick={(id) => {
			const preset = $presets.find((p) => p.id === id);
			if (preset) settings.set({ ...$settings, ...preset.settings });
		}}
		onsave={onsettings}
	/>

	<SettingsChips
		{options}
		{destination}
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
