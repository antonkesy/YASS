<script lang="ts">
	import { autoAnimate } from '$lib/autoAnimate';
	import { fade } from 'svelte/transition';
	import { ms } from '$lib/motion';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { useLayer } from '$lib/keys.svelte';
	import { hubError } from '$lib/hub/errors';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import type { ScannerDeviceDto, ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { overrideKeys, scanOverrides, settings, type ScanOverrides } from '$lib/settings';
	import Alert from '$lib/components/Alert.svelte';
	import Segmented from '$lib/components/Segmented.svelte';
	import Toggle from '$lib/components/Toggle.svelte';
	import SettingsSection from '$lib/components/SettingsSection.svelte';
	import TemplateEditor from '$lib/components/TemplateEditor.svelte';
	import ScannerList from '$lib/components/ScannerList.svelte';
	import FindScannersButton from '$lib/components/FindScannersButton.svelte';
	import PresetManager from '$lib/components/PresetManager.svelte';
	import ScanDefaultsForm from '$lib/components/ScanDefaultsForm.svelte';
	import AutoClearSetting from '$lib/components/AutoClearSetting.svelte';
	import { forget, remember, rename, scannerName, scannerRows, setHidden } from '$lib/scanners';
	import { Icon } from '$lib/icons';
	import {
		activePreset,
		DOCUMENT_META_FIELDS,
		DOCUMENT_META_LABELS,
		exportPrefs,
		pickedPreset,
		presets,
		reviewPrefs,
		scannerPrefs
	} from '$lib/presets';
	import { TILE_SIZES } from '$lib/review/overview';
	import { ExportFormat } from '$lib/hub/generated/Yass.Core.Hub';
	import { ColorMode } from '$lib/hub/generated/Yass.Core.Domain';
	import { colorLabel, exportFormatOptions } from '$lib/format';
	import { fileExtensions } from '$lib/review/download';
	import { expandTemplate, templateTokens, unknownTokens } from '$lib/template';
	import { t, type MessageKey } from '$lib/i18n/index.svelte';
	import { appearance, type Appearance } from '$lib/appearance.svelte';

	const client = getHub();
	const connection = client.connection;
	const sessions = client.sessions;

	let effective = $state<ScanOptionsDto | null>(null);
	let error = $state<string | null>(null);
	let devices = $state<ScannerDeviceDto[] | null>(null);

	const serverFolder = client.serverFolder;

	$effect(() => {
		if ($connection === 'connected' && !effective) {
			client.hub
				.getEffectiveOptions()
				.then((o) => (effective = o))
				.catch((e) => (error = hubError(e)));
			// Separately and non-fatal: enumerating SANE can take seconds.
			client.hub
				.listDevices()
				.then((d) => {
					devices = d;
					scannerPrefs.set(remember($scannerPrefs, d));
				})
				.catch(() => (devices = []));
		}
	});

	const tileNames: MessageKey[] = [
		'settings.tileTiny',
		'settings.tileSmall',
		'settings.tileMedium',
		'settings.tileLarge',
		'settings.tileHuge'
	];
	const tileSizes = $derived([
		{ value: 'fit', label: t('settings.tileFit') },
		...TILE_SIZES.map((size, i) => ({
			value: String(size),
			label: tileNames[i] ? t(tileNames[i]) : String(size)
		}))
	]);

	const appearances = $derived([
		{ value: 'auto', label: t('settings.appearanceAuto') },
		{ value: 'light', label: t('settings.appearanceLight') },
		{ value: 'dark', label: t('settings.appearanceDark') }
	]);

	const overrides = $derived($settings);
	const changedCount = $derived(overrideKeys.filter((key) => overrides[key] !== undefined).length);
	const changed = $derived(changedCount > 0);

	// Presets are matched and saved against the effective options, as on the home screen,
	// not the bare overrides.
	const planned = $derived<ScanOverrides>(
		effective ? { ...scanOverrides(effective), ...$settings } : $settings
	);
	const chosen = $derived(activePreset($presets, planned, $pickedPreset.id));
	const deviceLabel = (id: string) =>
		scannerName($scannerPrefs, id, devices?.find((d) => d.Id === id)?.Name);

	const currentDevice = $derived($settings.Device || effective?.Device || '');

	const previewFacts = $derived({
		preset: chosen?.name.replace(/\s+/g, '-') ?? 'Custom',
		counter: 42,
		dpi: $settings.Dpi ?? effective?.Dpi ?? 300,
		color: colorLabel($settings.Color ?? effective?.Color ?? ColorMode.Color)
	});
	const templatePreview = $derived(
		$exportPrefs.nameTemplate.trim()
			? expandTemplate($exportPrefs.nameTemplate, new Date(), previewFacts)
			: ''
	);

	const templatePresets = $derived(
		['{YEAR}-{MONTH}-{DAY}', '{YEAR}-{MONTH}-{DAY}_{HOUR}{MINUTE}', '{PRESET}-{COUNTER}'].map(
			(template) => ({
				template,
				example: expandTemplate(template, new Date(), previewFacts)
			})
		)
	);
	const templateUnknown = $derived(unknownTokens($exportPrefs.nameTemplate));

	function resetSettings() {
		settings.reset();
		pickedPreset.set({ id: null });
	}

	function close() {
		void goto(resolve('/'));
	}

	useLayer(close);
</script>

<svelte:head><title>{t('settings.pageTitle')}</title></svelte:head>

<div class="fixed inset-0 z-40 animate-fade-in bg-[rgb(0_0_0_/_0.35)] backdrop-blur-[3px]"></div>

<!-- The inner wrapper scrolls, inset from the edge, so the scrollbar misses the rounded corners. -->
<div
	class="fixed inset-x-0 bottom-0 z-41 flex max-h-[92%] animate-sheet-up flex-col overflow-hidden rounded-t-[28px] bg-surface shadow-modal sm:inset-x-auto sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:h-[calc(100%-48px)] sm:max-h-none sm:w-[min(1320px,calc(100%-48px))] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:animate-card-in sm:rounded-[26px]"
	role="dialog"
	aria-modal="true"
	aria-label={t('settings.title')}
>
	<header class="flex-none border-b border-line px-[22px] pt-3 pb-4 sm:px-[34px] sm:pt-6">
		<span class="mx-auto mb-3 block h-1 w-10 rounded-full bg-handle sm:hidden" aria-hidden="true"
		></span>
		<div class="flex items-center gap-2">
			<div class="flex min-w-0 flex-col">
				<h1 class="text-[20px] font-extrabold tracking-[-0.01em] sm:text-[24px]">
					{t('settings.title')}
				</h1>
			</div>
			<div class="flex-1"></div>
			{#if changed}
				<span
					class="flex h-7 items-center gap-1.5 rounded-full bg-accent-tint px-2.5 text-[12px] font-bold whitespace-nowrap text-accent-deep"
					transition:fade={{ duration: ms(180) }}
					title={t('settings.changedTitle')}
					data-testid="overrides-active"
				>
					<span class="h-1.5 w-1.5 rounded-full bg-accent"></span>
					{t('settings.changedCount', { n: changedCount })}
				</span>
			{/if}
			<button
				type="button"
				class="h-9 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash disabled:pointer-events-none disabled:text-ink-faint"
				onclick={resetSettings}
				disabled={!changed}
				title={t('settings.resetTitle')}
				data-testid="reset-settings"
			>
				{t('settings.reset')}
			</button>
			<button
				type="button"
				class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-ctl transition-colors hover:bg-ctl-hover"
				onclick={close}
				aria-label={t('settings.close')}
			>
				<Icon name="close" />
			</button>
		</div>
	</header>

	<div
		class="grid min-h-0 flex-1 items-start gap-y-5 overflow-y-auto overscroll-contain px-[22px] py-5 sm:mr-2.5 sm:pr-[24px] sm:pl-[34px] md:py-7"
		{@attach autoAnimate}
	>
		<SettingsSection title={t('settings.scanner')} icon="scan" testid="scanner-section">
			{#if error}
				<Alert class="col-span-full">{error}</Alert>
			{:else if !effective}
				<div class="col-span-full grid gap-3" aria-label={t('settings.loading')} role="status">
					{#each [0, 1, 2] as i (i)}
						<div class="h-[60px] animate-pulse rounded-[14px] bg-skeleton"></div>
					{/each}
				</div>
			{:else}
				<PresetManager {planned} {chosen} {currentDevice} {deviceLabel} />

				<ScanDefaultsForm {effective} />
			{/if}
		</SettingsSection>

		<SettingsSection title={t('settings.devices')} icon="scan" testid="scanners-section">
			<ScannerList
				rows={scannerRows($scannerPrefs, devices ?? [], sortedSessions($sessions))}
				chosen={currentDevice}
				onrename={(id, label) => scannerPrefs.set(rename($scannerPrefs, id, label))}
				onhide={(id, hidden) => scannerPrefs.set(setHidden($scannerPrefs, id, hidden))}
				onforget={(id) => scannerPrefs.set(forget($scannerPrefs, id))}
			/>
			{#if $connection === 'connected'}
				<div class="col-span-full -mt-2 flex">
					<FindScannersButton onfound={(d) => (devices = d)} />
				</div>
			{/if}
		</SettingsSection>

		<SettingsSection title={t('settings.export')} icon="download" testid="export-section">
			<Segmented
				name="format"
				label={t('settings.fileFormat')}
				hint={$exportPrefs.format === ExportFormat.Pdf
					? t('settings.perDocument')
					: t('settings.perPage')}
				options={exportFormatOptions}
				value={String($exportPrefs.format)}
				onchange={(v) => exportPrefs.set({ ...$exportPrefs, format: Number(v) as ExportFormat })}
			/>
			{#if $serverFolder}
				<div class="flex flex-col rounded-[16px] bg-app px-3.5 py-1.5">
					<Toggle
						name="server-save"
						label={t('settings.serverSave')}
						checked={$exportPrefs.serverSave}
						onchange={(on) => exportPrefs.set({ ...$exportPrefs, serverSave: on })}
					/>
					<span
						class="mb-2 truncate border-t border-line-strong pt-2 font-mono text-[12px] text-ink-muted"
						title={$serverFolder}
						data-testid="server-folder"
					>
						{$serverFolder}
					</span>
				</div>
			{/if}
		</SettingsSection>

		<!-- Outside the scanner section: must stay reachable while it is loading or has failed. -->
		<SettingsSection title={t('settings.naming')} icon="pencil" testid="naming-section">
			<div class="col-span-full flex flex-col rounded-[16px] bg-app px-3.5 py-1.5">
				<Toggle
					name="use-name-template"
					label={t('settings.useNameTemplate')}
					hint={$exportPrefs.useNameTemplate ? '' : t('settings.useNameTemplateOff')}
					checked={$exportPrefs.useNameTemplate}
					onchange={(on) => exportPrefs.set({ ...$exportPrefs, useNameTemplate: on })}
				/>
			</div>
			{#if $exportPrefs.useNameTemplate}
				<div class="col-span-full flex flex-col gap-1.5">
					<span class="text-[13px] font-bold text-ink-2">{t('settings.fileNameTemplate')}</span>
					<TemplateEditor
						value={$exportPrefs.nameTemplate}
						tokens={templateTokens}
						preview={templatePreview}
						presets={templatePresets}
						unknown={templateUnknown}
						extension={fileExtensions[$exportPrefs.format]}
						onchange={(v) => exportPrefs.set({ ...$exportPrefs, nameTemplate: v })}
					/>
				</div>
			{/if}
		</SettingsSection>

		<SettingsSection title={t('settings.metadata')} icon="tag" testid="metadata-section">
			{#each DOCUMENT_META_FIELDS as key (key)}
				<label class="flex flex-col gap-1.5">
					<span class="text-[13px] font-bold text-ink-2">{t(DOCUMENT_META_LABELS[key].label)}</span>
					<input
						class="field h-11 px-3 text-sm placeholder:text-ink-faint"
						type="text"
						name="metadata-{key}"
						autocomplete="off"
						placeholder={t(DOCUMENT_META_LABELS[key].placeholder)}
						value={$exportPrefs[key]}
						oninput={(e) => exportPrefs.set({ ...$exportPrefs, [key]: e.currentTarget.value })}
						data-testid="metadata-{key}"
					/>
				</label>
			{/each}
		</SettingsSection>

		<SettingsSection title={t('settings.editing')} icon="grid" testid="editing-section">
			<div class="col-span-full flex flex-col gap-1.5">
				<Segmented
					name="overview-tile"
					label={t('settings.tileSize')}
					options={tileSizes}
					value={String($reviewPrefs.overviewTile)}
					onchange={(v) =>
						reviewPrefs.set({ ...$reviewPrefs, overviewTile: v === 'fit' ? 'fit' : Number(v) })}
				/>
			</div>
		</SettingsSection>

		<SettingsSection title={t('settings.appearance')} icon="contrast" testid="appearance-section">
			<div class="col-span-full flex flex-col gap-1.5">
				<Segmented
					name="appearance"
					label={t('settings.appearance')}
					hint={t('settings.appearanceHint')}
					options={appearances}
					value={appearance.mode}
					onchange={(v) => appearance.set(v as Appearance)}
				/>
			</div>
		</SettingsSection>

		<SettingsSection title={t('settings.pastScans')} icon="archive" testid="history-section">
			<AutoClearSetting />
		</SettingsSection>
	</div>

	<footer
		class="flex flex-none justify-end border-t border-line px-[22px] pt-3.5 pb-[max(1.25rem,env(safe-area-inset-bottom))] sm:px-[34px] sm:pb-5"
	>
		<button
			type="button"
			class="h-12 w-full rounded-[14px] bg-accent text-[15px] font-extrabold text-white shadow-done transition-[background-color,box-shadow] duration-200 hover:bg-accent-hover hover:shadow-export-hover sm:w-[200px]"
			onclick={close}
		>
			{t('settings.done')}
		</button>
	</footer>
</div>
