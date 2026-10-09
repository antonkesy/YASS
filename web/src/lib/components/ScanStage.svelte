<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { pickKeyframes, play } from '$lib/motion';
	import { t } from '$lib/i18n/index.svelte';
	import type {
		ScannerDeviceDto,
		ScanOptionsDto,
		ScannerStatusDto
	} from '$lib/hub/generated/Yass.Core.Hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import type { ConnectionState } from '$lib/hub/client';
	import { pageSizeLabel } from '$lib/format';
	import {
		activePreset,
		applyPreset,
		exportPrefs,
		pickedPreset,
		presets,
		presetsFor
	} from '$lib/presets';
	import { exportFormatLabels } from '$lib/format';
	import { settings } from '$lib/settings';
	import PaperStack from './PaperStack.svelte';
	import PresetPills from './PresetPills.svelte';
	import ScanButton from './ScanButton.svelte';
	import SettingsChips from './SettingsChips.svelte';
	import { Icon } from '$lib/icons';
	import ScannerDetails from './ScannerDetails.svelte';

	let {
		status,
		connection,
		options,
		devices = [],
		deviceName = null,
		busy = false,
		onscan,
		onsettings,
		onsavepreset
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
		/** Opens Settings ready to name the current settings as a preset; falls back to plain Settings. */
		onsavepreset?: () => void;
	} = $props();

	/**
	 * The lit pill's details panel: hovering the pill shows it until the pointer leaves,
	 * a press pins it until a press outside.
	 */
	let details = $state<'hover' | 'pinned' | null>(null);

	/**
	 * The scanner pill that lights up bounces, as a preset does when picked. Not on load: a page
	 * that opens with a scanner already chosen has nothing to answer.
	 */
	let settled = false;
	$effect(() => {
		settled = true;
	});
	function bounceWhenPicked(id: string) {
		return (el: Element) => {
			if (shown === id && settled) play(el, pickKeyframes, 420);
		};
	}

	/** The scanner the next scan will run on: this browser's pick, or the server's. */
	const device = $derived(options?.Device || '');

	/**
	 * Only once there is something to show: the line above already says when no scanner was
	 * found. yass.yaml has no say in it — the file supplies the default scanner, the person
	 * at the screen chooses.
	 */
	const canPickDevice = $derived(devices.length > 0);

	/**
	 * What the box shows. There is no "Automatic" entry: a scanner is always named, which
	 * is this browser's pick when it has one, otherwise the scanner the server says it is
	 * on, and failing that the first one listed. The server's status names it rather than
	 * identifying it, so the match is by name.
	 */
	const shown = $derived(
		device || devices.find((d) => d.Name === status?.DeviceName)?.Id || devices[0]?.Id || ''
	);

	/**
	 * One the server has stopped listing still has to show, or the row would silently
	 * light another scanner while the scan goes somewhere else.
	 */
	const listed = $derived(
		device && !devices.some((d) => d.Id === device)
			? [...devices, { Id: device, Name: t('home.deviceNotFound', { device }) }]
			: devices
	);

	/** Presets follow the scanner, so a scanner-specific one is out of sight on the others. */
	const offered = $derived(presetsFor($presets, device || undefined));

	// Matched against what the scan will do, not against the stored overrides alone:
	// a browser with no overrides still matches whichever preset the server defaults to.
	const chosen = $derived(options ? activePreset(offered, options, $pickedPreset.id) : null);
	/** The preset picked last, once a setting has moved away from it. */
	const edited = $derived(
		chosen || !options ? null : (offered.find((p) => p.id === $pickedPreset.id) ?? null)
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
				? t('home.blockedConnecting')
				: t('home.blockedOffline')
			: status?.State === ScannerState.Busy
				? t('home.blockedBusy')
				: status?.State === ScannerState.NoDevice
					? t('home.blockedNoDevice')
					: status?.State === ScannerState.Error
						? t('home.blockedError')
						: t('home.blockedChecking')
	);

	const caption = $derived(
		[pageSizeLabel(options?.PageSize), options?.Dpi ? t('home.dpi', { n: options.Dpi }) : '']
			.filter(Boolean)
			.join(' · ')
	);
</script>

<section
	class="flex min-h-0 animate-fade-in flex-col items-center justify-center gap-[clamp(8px,1.4vh,18px)] overflow-y-auto rounded-[28px] bg-surface px-5 py-8 shadow-stage sm:px-8 sm:py-[clamp(1rem,2.6vh,3rem)]"
	style:flex="2 1 520px"
	data-testid="scan-stage"
	{@attach autoAnimate}
>
	<PaperStack {caption} pulse={chosen?.id ?? 'custom'} />

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
		<!-- Every scanner in a row, the chosen one lit, the same way the presets are picked. -->
		<div class="flex flex-col items-center gap-2" data-testid="device-picker">
			<span class="text-[13px] font-semibold text-ink-muted">{t('home.scanner')}</span>
			<div
				class="flex flex-wrap justify-center gap-1.5 rounded-[24px] bg-ctl-track p-[4px]"
				role="radiogroup"
				aria-label={t('home.scanner')}
				data-testid="device-pills"
			>
				{#each listed as scanner (scanner.Id)}
					{@const lit = shown === scanner.Id}
					<!-- Pressing a pill picks that scanner; pressing the lit one again opens its details. -->
					<div
						class="relative"
						role="presentation"
						onmouseenter={() => {
							if (lit && details === null) details = 'hover';
						}}
						onmouseleave={() => {
							if (details === 'hover') details = null;
						}}
					>
						<button
							type="button"
							role="radio"
							class="flex h-[38px] max-w-[min(340px,80vw)] items-center rounded-full px-3.5 text-[13px] font-bold transition-[background-color,color,box-shadow,transform] duration-200 ease-spring active:scale-[0.97] {lit
								? 'bg-seg text-ink shadow-seg'
								: 'text-ink-2 hover:text-ink'}"
							aria-checked={lit}
							onclick={() => {
								if (lit) details = details === 'pinned' ? null : 'pinned';
								else settings.set({ ...$settings, Device: scanner.Id });
							}}
							data-device={scanner.Id}
							{@attach bounceWhenPicked(scanner.Id)}
						>
							<span class="truncate">{scanner.Name}</span>
						</button>
						{#if lit && details !== null}
							<ScannerDetails
								{status}
								{connection}
								name={scanner.Name}
								pinned={details === 'pinned'}
								onclose={() => (details = null)}
							/>
						{/if}
					</div>
				{/each}
			</div>
		</div>
	{/if}

	<PresetPills
		presets={offered}
		active={chosen?.id ?? null}
		edited={edited?.id ?? null}
		modified={chosen === null}
		onpick={(id) => {
			const preset = offered.find((p) => p.id === id);
			if (!preset) return;
			settings.set(applyPreset(preset, $settings));
			pickedPreset.set({ id });
		}}
		onsave={onsavepreset ?? onsettings}
	/>

	<SettingsChips
		{options}
		deviceName={canPickDevice ? null : deviceName}
		format={exportFormatLabels[$exportPrefs.format]}
		onclick={onsettings}
	/>

	<button
		type="button"
		onclick={onsettings}
		class="flex h-11 w-11 items-center justify-center rounded-full text-accent transition-colors hover:bg-accent-wash"
		aria-label={t('home.changeSettings')}
		title={t('home.changeSettings')}
		data-testid="change-settings"
	>
		<Icon name="sliders" />
	</button>

	<ScanButton disabled={!ready || busy} {busy} reason={blocked} onclick={onscan} />
</section>
