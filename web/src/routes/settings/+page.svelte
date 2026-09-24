<script lang="ts">
	import { resolve } from '$app/paths';
	import { getHub } from '$lib/hub/hub';
	import { ColorMode, PageSize, ScanSource } from '$lib/hub/generated/Yawss.Core.Domain';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yawss.Core.Hub';
	import { colorModeLabels } from '$lib/review/edits';
	import { editableKeys, settings } from '$lib/settings';

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

	function num(e: Event): number | undefined {
		const v = (e.currentTarget as HTMLInputElement | HTMLSelectElement).value;
		return v === '' ? undefined : Number(v);
	}

	const sources: [ScanSource, string][] = [
		[ScanSource.Feeder, 'Feeder (ADF)'],
		[ScanSource.Duplex, 'Feeder, both sides'],
		[ScanSource.Flatbed, 'Flatbed']
	];
	const pageSizes: [PageSize, string][] = [
		[PageSize.A4, 'A4'],
		[PageSize.A5, 'A5'],
		[PageSize.Letter, 'Letter'],
		[PageSize.Legal, 'Legal']
	];
	const dpis = [150, 200, 300, 400, 600];

	const field = 'flex min-h-11 flex-col gap-1 py-2';
	const input = 'min-h-11 rounded-lg border border-stone-300 bg-white px-3';
</script>

<svelte:head><title>Settings · YAWSS</title></svelte:head>

<main class="mx-auto flex max-w-lg flex-col gap-4 px-4 pt-3 pb-12">
	<header class="flex items-center gap-2">
		<a
			href={resolve('/')}
			class="flex min-h-11 min-w-11 items-center justify-center rounded-full text-stone-700 hover:bg-stone-200"
			aria-label="Back to home"
		>
			<svg
				class="h-6 w-6"
				viewBox="0 0 24 24"
				fill="none"
				stroke="currentColor"
				stroke-width="2"
				aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg
			>
		</a>
		<h1 class="text-xl font-bold">Scan settings</h1>
	</header>

	<p class="text-sm text-stone-600">
		Stored in this browser only. Keys not listed here are fixed in <code>yawss.yaml</code>.
	</p>

	{#if error}
		<p class="rounded-lg bg-rose-100 px-4 py-2 text-rose-800" role="alert">{error}</p>
	{:else if !effective}
		<p class="text-stone-500">Loading…</p>
	{:else if editable.size === 0}
		<p class="text-stone-500" data-testid="nothing-editable">Nothing is editable from the UI.</p>
	{:else}
		<form
			class="flex flex-col divide-y divide-stone-200"
			onsubmit={(e) => e.preventDefault()}
			data-testid="settings-form"
		>
			{#if editable.has('source')}
				<label class={field}>
					<span
						>Source <span class="text-stone-500"
							>(default: {sources.find(([v]) => v === effective?.Source)?.[1]})</span
						></span
					>
					<select
						class={input}
						name="source"
						value={overrides.Source ?? ''}
						onchange={(e) => settings.update('Source', num(e) as ScanSource | undefined)}
					>
						<option value="">Use default</option>
						{#each sources as [value, label] (value)}<option {value}>{label}</option>{/each}
					</select>
				</label>
			{/if}
			{#if editable.has('dpi')}
				<label class={field}>
					<span>Resolution <span class="text-stone-500">(default: {effective.Dpi} dpi)</span></span>
					<select
						class={input}
						name="dpi"
						value={overrides.Dpi ?? ''}
						onchange={(e) => settings.update('Dpi', num(e))}
					>
						<option value="">Use default</option>
						{#each dpis as dpi (dpi)}<option value={dpi}>{dpi} dpi</option>{/each}
					</select>
				</label>
			{/if}
			{#if editable.has('color')}
				<label class={field}>
					<span
						>Colour <span class="text-stone-500"
							>(default: {colorModeLabels[effective.Color ?? ColorMode.Grayscale]})</span
						></span
					>
					<select
						class={input}
						name="color"
						value={overrides.Color ?? ''}
						onchange={(e) => settings.update('Color', num(e) as ColorMode | undefined)}
					>
						<option value="">Use default</option>
						{#each Object.entries(colorModeLabels) as [value, label] (value)}<option
								value={Number(value)}>{label}</option
							>{/each}
					</select>
				</label>
			{/if}
			{#if editable.has('page_size')}
				<label class={field}>
					<span
						>Page size <span class="text-stone-500"
							>(default: {pageSizes.find(([v]) => v === effective?.PageSize)?.[1]})</span
						></span
					>
					<select
						class={input}
						name="page_size"
						value={overrides.PageSize ?? ''}
						onchange={(e) => settings.update('PageSize', num(e) as PageSize | undefined)}
					>
						<option value="">Use default</option>
						{#each pageSizes as [value, label] (value)}<option {value}>{label}</option>{/each}
					</select>
				</label>
			{/if}
			{#if editable.has('brightness')}
				<label class={field}>
					<span>Brightness: {overrides.Brightness ?? effective.Brightness}</span>
					<input
						class="min-h-11"
						type="range"
						name="brightness"
						min="-100"
						max="100"
						value={overrides.Brightness ?? effective.Brightness}
						oninput={(e) => settings.update('Brightness', num(e))}
					/>
				</label>
			{/if}
			{#if editable.has('contrast')}
				<label class={field}>
					<span>Contrast: {overrides.Contrast ?? effective.Contrast}</span>
					<input
						class="min-h-11"
						type="range"
						name="contrast"
						min="-100"
						max="100"
						value={overrides.Contrast ?? effective.Contrast}
						oninput={(e) => settings.update('Contrast', num(e))}
					/>
				</label>
			{/if}
			{#if editable.has('blank_page_detection')}
				<label class="flex min-h-11 items-center justify-between gap-3 py-2">
					<span
						>Skip blank pages <span class="text-stone-500"
							>(default: {effective.BlankPageDetection ? 'on' : 'off'})</span
						></span
					>
					<input
						class="h-6 w-6 accent-teal-700"
						type="checkbox"
						name="blank_page_detection"
						checked={overrides.BlankPageDetection ?? effective.BlankPageDetection}
						onchange={(e) =>
							settings.update('BlankPageDetection', (e.currentTarget as HTMLInputElement).checked)}
					/>
				</label>
			{/if}
			{#if editable.has('blank_page_threshold')}
				<label class={field}>
					<span
						>Blank threshold <span class="text-stone-500"
							>(default: {effective.BlankPageThreshold})</span
						></span
					>
					<input
						class={input}
						type="number"
						name="blank_page_threshold"
						min="0"
						max="1"
						step="0.01"
						value={overrides.BlankPageThreshold ?? ''}
						placeholder={String(effective.BlankPageThreshold)}
						onchange={(e) => settings.update('BlankPageThreshold', num(e))}
					/>
				</label>
			{/if}
			{#if editable.has('auto_deskew')}
				<label class="flex min-h-11 items-center justify-between gap-3 py-2">
					<span
						>Auto deskew <span class="text-stone-500"
							>(default: {effective.AutoDeskew ? 'on' : 'off'})</span
						></span
					>
					<input
						class="h-6 w-6 accent-teal-700"
						type="checkbox"
						name="auto_deskew"
						checked={overrides.AutoDeskew ?? effective.AutoDeskew}
						onchange={(e) =>
							settings.update('AutoDeskew', (e.currentTarget as HTMLInputElement).checked)}
					/>
				</label>
			{/if}
		</form>
		<button
			type="button"
			class="min-h-11 self-start rounded-lg border border-stone-300 px-4 font-medium hover:bg-stone-100"
			onclick={() => settings.reset()}
			data-testid="reset-settings"
		>
			Reset to defaults
		</button>
		<p class="text-xs text-stone-500">
			Editable keys: {[...editable].join(', ')} — configured by <code>scanner.ui_editable</code>.
		</p>
		{#if Object.keys(editableKeys).some((k) => editable.has(k) && overrides[editableKeys[k]] !== undefined)}
			<p class="text-sm text-teal-800" data-testid="overrides-active">
				Overrides active for the next scan.
			</p>
		{/if}
	{/if}
</main>
