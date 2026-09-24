<script lang="ts">
	import { ColorMode } from '$lib/hub/generated/Yawss.Core.Domain';
	import { colorModeLabels } from '$lib/review/edits';

	let {
		selectedCount,
		total,
		anyDeleted,
		disabled = false,
		onrotate,
		ondelete,
		onrestore,
		oncolor,
		onclear
	}: {
		selectedCount: number;
		total: number;
		anyDeleted: boolean;
		disabled?: boolean;
		onrotate: (degrees: 90 | -90) => void;
		ondelete: () => void;
		onrestore: () => void;
		oncolor: (mode: ColorMode | undefined) => void;
		onclear: () => void;
	} = $props();

	const scope = $derived(selectedCount === 0 ? `all ${total}` : `${selectedCount} selected`);
	const btn =
		'flex min-h-11 min-w-11 items-center justify-center gap-1 rounded-lg px-3 text-sm font-medium text-stone-800 hover:bg-stone-200 focus-visible:ring-4 focus-visible:ring-teal-300 focus-visible:outline-none disabled:opacity-40';
</script>

<div
	class="flex flex-wrap items-center gap-1 rounded-xl border border-stone-200 bg-white p-1 shadow-sm"
	role="toolbar"
	aria-label="Page edits"
	data-testid="toolbar"
>
	<span class="px-2 text-sm text-stone-600" data-testid="toolbar-scope">Acting on {scope}</span>
	{#if selectedCount > 0}
		<button type="button" class={btn} onclick={onclear}>Clear</button>
	{/if}
	<span class="mx-1 h-6 w-px bg-stone-200" aria-hidden="true"></span>
	<button
		type="button"
		class={btn}
		{disabled}
		onclick={() => onrotate(-90)}
		aria-label="Rotate left"
		title="Rotate left"
	>
		<svg
			class="h-5 w-5"
			viewBox="0 0 24 24"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			aria-hidden="true"><path d="M3 12a9 9 0 1 0 3-6.7M3 4v5h5" /></svg
		>
	</button>
	<button
		type="button"
		class={btn}
		{disabled}
		onclick={() => onrotate(90)}
		aria-label="Rotate right"
		title="Rotate right"
	>
		<svg
			class="h-5 w-5"
			viewBox="0 0 24 24"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			aria-hidden="true"><path d="M21 12a9 9 0 1 1-3-6.7M21 4v5h-5" /></svg
		>
	</button>
	<button type="button" class={btn} {disabled} onclick={ondelete} data-testid="delete-button"
		>Delete</button
	>
	<button
		type="button"
		class={btn}
		disabled={disabled || !anyDeleted}
		onclick={onrestore}
		data-testid="restore-button">Restore</button
	>
	<label class="flex min-h-11 items-center gap-1 px-2 text-sm">
		<span class="sr-only">Colour mode</span>
		<select
			class="min-h-9 rounded-md border border-stone-300 bg-white px-2"
			{disabled}
			data-testid="color-select"
			onchange={(e) => {
				const v = (e.currentTarget as HTMLSelectElement).value;
				oncolor(v === '' ? undefined : (Number(v) as ColorMode));
				(e.currentTarget as HTMLSelectElement).value = '';
			}}
		>
			<option value="">Colour…</option>
			<option value="">Scan default</option>
			{#each Object.values(ColorMode).filter((v) => typeof v === 'number') as mode (mode)}
				<option value={mode}>{colorModeLabels[mode as ColorMode]}</option>
			{/each}
		</select>
	</label>
</div>
