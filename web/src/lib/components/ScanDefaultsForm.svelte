<script lang="ts">
	import { ColorMode, PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, pageSizeLabel, sourceLabel } from '$lib/format';
	import { t } from '$lib/i18n/index.svelte';
	import { settings } from '$lib/settings';
	import Segmented from './Segmented.svelte';
	import Slider from './Slider.svelte';
	import Toggle from './Toggle.svelte';

	let { effective }: { effective: ScanOptionsDto } = $props();

	const overrides = $derived($settings);

	const defaultHint = (label: string | undefined) =>
		label ? t('settings.defaultHint', { value: label }) : '';

	// "" is the segment that clears the override and falls back to yass.yaml.
	const DEFAULT = '';
	const asSegment = (v: number | boolean | undefined) => (v === undefined ? DEFAULT : String(v));

	const sources = $derived([
		{ value: String(ScanSource.Flatbed), label: t('settings.flatbed') },
		{ value: String(ScanSource.Feeder), label: t('settings.feeder') },
		{ value: String(ScanSource.Duplex), label: t('settings.bothSides') }
	]);
	const colors = $derived([
		{ value: String(ColorMode.Bw), label: t('settings.bw') },
		{ value: String(ColorMode.Grayscale), label: t('settings.grayscale') },
		{ value: String(ColorMode.Color), label: t('settings.color') }
	]);
	const dpis = [150, 200, 300, 400, 600].map((d) => ({ value: String(d), label: String(d) }));
	const pageSizes = $derived([
		{ value: String(PageSize.A4), label: 'A4' },
		{ value: String(PageSize.A5), label: 'A5' },
		{ value: String(PageSize.Letter), label: t('settings.letter') },
		{ value: String(PageSize.Legal), label: t('settings.legal') }
	]);
	const withDefault = (options: { value: string; label: string }[]) => [
		{ value: DEFAULT, label: t('settings.default') },
		...options
	];

	function setNumber<K extends 'Source' | 'Dpi' | 'Color' | 'PageSize'>(key: K, raw: string) {
		settings.update(key, raw === DEFAULT ? undefined : (Number(raw) as never));
	}
</script>

<form
	class="col-span-full grid items-start gap-x-6 gap-y-5 sm:grid-cols-2"
	onsubmit={(e) => e.preventDefault()}
	data-testid="settings-form"
>
	<Segmented
		name="source"
		label={t('settings.source')}
		hint={defaultHint(sourceLabel(effective.Source))}
		options={withDefault(sources)}
		value={asSegment(overrides.Source)}
		onchange={(v) => setNumber('Source', v)}
	/>
	<Segmented
		name="color"
		label={t('settings.colourMode')}
		hint={defaultHint(colorLabel(effective.Color))}
		options={withDefault(colors)}
		value={asSegment(overrides.Color)}
		onchange={(v) => setNumber('Color', v)}
	/>
	<Segmented
		name="dpi"
		label={t('settings.resolution')}
		hint={effective.Dpi ? t('settings.defaultDpi', { dpi: effective.Dpi }) : t('settings.dpi')}
		options={withDefault(dpis)}
		value={asSegment(overrides.Dpi)}
		onchange={(v) => setNumber('Dpi', v)}
	/>
	<Segmented
		name="page_size"
		label={t('settings.paperSize')}
		hint={defaultHint(pageSizeLabel(effective.PageSize))}
		options={withDefault(pageSizes)}
		value={asSegment(overrides.PageSize)}
		onchange={(v) => setNumber('PageSize', v)}
	/>

	<fieldset class="flex flex-col rounded-[16px] bg-app px-3.5 py-1.5">
		<legend class="sr-only">{t('settings.cleanUp')}</legend>
		<Toggle
			name="blank_page_detection"
			label={t('settings.skipBlank')}
			checked={overrides.BlankPageDetection ?? effective.BlankPageDetection ?? false}
			onchange={(v) => settings.update('BlankPageDetection', v)}
		/>
		<label
			class="mb-2 ml-3 flex items-center justify-between gap-3 border-l-2 border-line-strong pl-3 transition-opacity {(overrides.BlankPageDetection ??
			effective.BlankPageDetection)
				? ''
				: 'opacity-50'}"
		>
			<span class="flex min-w-0 flex-col">
				<span class="text-[13px] font-bold text-ink-2">{t('settings.blankThreshold')}</span>
				<span class="text-xs text-ink-muted">
					{t('settings.defaultHint', { value: effective.BlankPageThreshold ?? '' })}
				</span>
			</span>
			<input
				class="field h-10 w-[84px] flex-none px-2.5 text-center font-mono text-sm"
				type="number"
				name="blank_page_threshold"
				min="0"
				max="1"
				step="0.01"
				value={overrides.BlankPageThreshold ?? ''}
				placeholder={String(effective.BlankPageThreshold)}
				onchange={(e) => {
					const v = (e.currentTarget as HTMLInputElement).value;
					settings.update('BlankPageThreshold', v === '' ? undefined : Number(v));
				}}
			/>
		</label>
		<div class="border-t border-line-strong"></div>
		<Toggle
			name="auto_deskew"
			label={t('settings.autoDeskew')}
			checked={overrides.AutoDeskew ?? effective.AutoDeskew ?? false}
			onchange={(v) => settings.update('AutoDeskew', v)}
		/>
	</fieldset>

	<fieldset class="flex flex-col gap-1 rounded-[16px] bg-app px-3.5 py-2.5">
		<legend class="sr-only">{t('settings.imageAdjustments')}</legend>
		{#each [['brightness', 'settings.brightness', 'Brightness'], ['contrast', 'settings.contrast', 'Contrast']] as const as [key, label, prop] (key)}
			<Slider
				name={key}
				label={t(label)}
				value={overrides[prop] ?? effective[prop] ?? 0}
				onchange={(v) => settings.update(prop, v)}
			/>
		{/each}
	</fieldset>
</form>
