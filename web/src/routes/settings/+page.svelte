<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { getHub } from '$lib/hub/hub';
	import { PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorModeLabels } from '$lib/review/edits';
	import { editableKeys, settings } from '$lib/settings';
	import Segmented from '$lib/components/Segmented.svelte';
	import Toggle from '$lib/components/Toggle.svelte';
	import { Icon } from '$lib/icons';

	const client = getHub();
	const connection = client.connection;

	let effective = $state<ScanOptionsDto | null>(null);
	let error = $state<string | null>(null);

	$effect(() => {
		if ($connection === 'connected' && !effective) {
			client.hub
				.getEffectiveOptions()
				.then((o) => (effective = o))
				.catch((e) => (error = e instanceof Error ? e.message : String(e)));
		}
	});

	const editable = $derived(new Set(effective?.Editable ?? []));
	const overrides = $derived($settings);
	const changed = $derived(
		Object.keys(editableKeys).some(
			(k) => editable.has(k) && overrides[editableKeys[k]] !== undefined
		)
	);

	// "" is the segment that clears the override and falls back to yass.yaml.
	const DEFAULT = '';
	const asSegment = (v: number | boolean | undefined) => (v === undefined ? DEFAULT : String(v));

	const sources = [
		{ value: String(ScanSource.Flatbed), label: 'Flatbed' },
		{ value: String(ScanSource.Feeder), label: 'Feeder' },
		{ value: String(ScanSource.Duplex), label: 'Both sides' }
	];
	const colors = Object.entries(colorModeLabels).map(([value, label]) => ({ value, label }));
	const dpis = [150, 200, 300, 400, 600].map((d) => ({ value: String(d), label: String(d) }));
	const pageSizes = [
		{ value: String(PageSize.A4), label: 'A4' },
		{ value: String(PageSize.A5), label: 'A5' },
		{ value: String(PageSize.Letter), label: 'Letter' },
		{ value: String(PageSize.Legal), label: 'Legal' }
	];
	const withDefault = (options: { value: string; label: string }[]) => [
		{ value: DEFAULT, label: 'Default' },
		...options
	];

	function close() {
		void goto(resolve('/'));
	}

	function setNumber<K extends 'Source' | 'Dpi' | 'Color' | 'PageSize'>(key: K, raw: string) {
		settings.update(key, raw === DEFAULT ? undefined : (Number(raw) as never));
	}
</script>

<svelte:head><title>Settings · YASS</title></svelte:head>

<!--
	The route stays, but it reads as the mockup's sheet: a centred card on a desktop
	and a bottom sheet on a phone, over a dimmed page.
-->
<div class="fixed inset-0 z-40 bg-[rgb(19_26_43_/_0.35)] backdrop-blur-[3px]"></div>

<div
	class="fixed inset-x-0 bottom-0 z-41 max-h-[86%] animate-pop-in overflow-y-auto rounded-t-[28px] bg-surface px-[22px] pt-[22px] pb-6 shadow-modal sm:inset-x-auto sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:max-h-[calc(100%-48px)] sm:w-[min(920px,calc(100%-64px))] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-[26px] sm:px-[34px] sm:py-[30px]"
	role="dialog"
	aria-modal="true"
	aria-label="Scan settings"
>
	<span class="mx-auto mb-3 block h-1 w-10 rounded-full bg-handle sm:hidden" aria-hidden="true"
	></span>

	<div
		class="grid items-start gap-x-7 gap-y-[18px] sm:grid-cols-[repeat(auto-fit,minmax(320px,1fr))]"
	>
		<div class="col-span-full flex items-center gap-2">
			<h1 class="text-[20px] font-extrabold tracking-[-0.01em] sm:text-[26px]">Scan settings</h1>
			<div class="flex-1"></div>
			<button
				type="button"
				class="h-8 rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
				onclick={() => settings.reset()}
				data-testid="reset-settings"
			>
				Reset
			</button>
			<button
				type="button"
				class="flex h-9 w-9 items-center justify-center rounded-full bg-ctl transition-colors hover:bg-ctl-hover"
				onclick={close}
				aria-label="Close settings"
			>
				<Icon name="close" />
			</button>
		</div>

		<p class="col-span-full text-[13px] text-ink-muted">
			Stored in this browser only. Keys not listed here are fixed in
			<code class="font-mono">yass.yaml</code>.
		</p>

		{#if error}
			<p
				class="col-span-full rounded-[14px] bg-danger-bg px-4 py-2 font-semibold text-danger"
				role="alert"
			>
				{error}
			</p>
		{:else if !effective}
			<p class="col-span-full text-ink-muted">Loading…</p>
		{:else if editable.size === 0}
			<p class="col-span-full text-ink-muted" data-testid="nothing-editable">
				Nothing is editable from the UI.
			</p>
		{:else}
			<form
				class="col-span-full grid items-start gap-x-7 gap-y-[18px] sm:grid-cols-[repeat(auto-fit,minmax(320px,1fr))]"
				onsubmit={(e) => e.preventDefault()}
				data-testid="settings-form"
			>
				{#if editable.has('source')}
					<Segmented
						name="source"
						label="Source"
						options={withDefault(sources)}
						value={asSegment(overrides.Source)}
						onchange={(v) => setNumber('Source', v)}
					/>
				{/if}
				{#if editable.has('color')}
					<Segmented
						name="color"
						label="Colour mode"
						options={withDefault(colors)}
						value={asSegment(overrides.Color)}
						onchange={(v) => setNumber('Color', v)}
					/>
				{/if}
				{#if editable.has('dpi')}
					<Segmented
						name="dpi"
						label="Resolution"
						hint="dpi"
						options={withDefault(dpis)}
						value={asSegment(overrides.Dpi)}
						onchange={(v) => setNumber('Dpi', v)}
					/>
				{/if}
				{#if editable.has('page_size')}
					<Segmented
						name="page_size"
						label="Paper size"
						options={withDefault(pageSizes)}
						value={asSegment(overrides.PageSize)}
						onchange={(v) => setNumber('PageSize', v)}
					/>
				{/if}

				{#if editable.has('blank_page_detection') || editable.has('auto_deskew')}
					<div class="col-span-full rounded-[16px] bg-app px-3.5 py-1.5">
						{#if editable.has('blank_page_detection')}
							<Toggle
								name="blank_page_detection"
								label="Skip blank pages"
								hint="Pages that look empty start out deleted"
								checked={overrides.BlankPageDetection ?? effective.BlankPageDetection ?? false}
								onchange={(v) => settings.update('BlankPageDetection', v)}
							/>
						{/if}
						{#if editable.has('auto_deskew')}
							<Toggle
								name="auto_deskew"
								label="Auto-deskew"
								hint="Straighten pages that went through crooked"
								checked={overrides.AutoDeskew ?? effective.AutoDeskew ?? false}
								onchange={(v) => settings.update('AutoDeskew', v)}
							/>
						{/if}
					</div>
				{/if}

				{#each [['brightness', 'Brightness', 'Brightness'], ['contrast', 'Contrast', 'Contrast']] as const as [key, label, prop] (key)}
					{#if editable.has(key)}
						<label class="flex flex-col gap-1.5">
							<span class="flex justify-between text-[13px] font-bold text-ink-2">
								{label}
								<span class="text-ink-muted tabular-nums">
									{overrides[prop] ?? effective[prop] ?? 0}
								</span>
							</span>
							<input
								class="h-11 accent-accent"
								type="range"
								name={key}
								min="-100"
								max="100"
								step="5"
								value={overrides[prop] ?? effective[prop] ?? 0}
								oninput={(e) =>
									settings.update(prop, Number((e.currentTarget as HTMLInputElement).value))}
							/>
						</label>
					{/if}
				{/each}

				{#if editable.has('blank_page_threshold')}
					<label class="flex flex-col gap-1.5">
						<span class="text-[13px] font-bold text-ink-2">
							Blank threshold
							<span class="font-semibold text-ink-muted">
								· default {effective.BlankPageThreshold}
							</span>
						</span>
						<input
							class="h-11 rounded-[12px] bg-ctl px-3 font-mono text-sm"
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
				{/if}
			</form>

			<p class="col-span-full text-[11px] text-ink-faint">
				Editable keys: {[...editable].join(', ')} — set by
				<code class="font-mono">scanner.ui_editable</code>.
			</p>

			{#if changed}
				<p
					class="col-span-full rounded-[12px] bg-success-bg px-3 py-2.5 text-xs font-bold text-success-ink"
					data-testid="overrides-active"
				>
					Overrides active for the next scan.
				</p>
			{/if}
		{/if}

		<button
			type="button"
			class="col-span-full h-12 w-full justify-self-end rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 text-[15px] font-extrabold text-white shadow-done transition-shadow duration-200 hover:shadow-export-hover sm:w-[220px]"
			onclick={close}
		>
			Done
		</button>
	</div>
</div>
