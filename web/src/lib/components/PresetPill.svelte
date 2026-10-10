<script lang="ts">
	import type { HTMLButtonAttributes } from 'svelte/elements';
	import { Icon } from '$lib/icons';
	import { presetName, type Preset } from '$lib/presets';

	let {
		preset,
		active,
		edited,
		editedTitle,
		note = '',
		tray = false,
		onclick,
		...rest
	}: {
		preset: Preset;
		/** The preset the next scan is running. */
		active: boolean;
		/** Picked last, and changed away from since: marked with a dot and outlined. */
		edited: boolean;
		/** Why the edited one is marked, as its tooltip. */
		editedTitle: string;
		/** A word after the name, such as the scanner a preset belongs to. */
		note?: string;
		/**
		 * On the home screen's tray, which is its own hover: the pill only darkens its text, and
		 * springs when pressed.
		 */
		tray?: boolean;
		onclick: () => void;
	} & Omit<HTMLButtonAttributes, 'class' | 'title' | 'onclick'> = $props();
</script>

<button
	type="button"
	class="relative flex h-[38px] items-center gap-2 rounded-full px-3.5 text-[13px] font-bold duration-200 {tray
		? 'transition-[background-color,color,box-shadow,transform] ease-spring active:scale-[0.97]'
		: 'transition-all'} {active
		? 'bg-seg text-ink shadow-seg'
		: edited
			? 'text-ink-2 shadow-ring-outline hover:text-ink'
			: tray
				? 'text-ink-2 hover:text-ink'
				: 'text-ink-2 hover:bg-ctl-hover hover:text-ink'}"
	aria-pressed={active}
	title={edited ? editedTitle : undefined}
	{onclick}
	{...rest}
>
	{#if edited}
		<span
			class="absolute top-0.5 right-1 h-2 w-2 rounded-full bg-warning ring-2 ring-surface"
			aria-hidden="true"
		></span>
	{/if}
	<Icon name={preset.icon} />
	{presetName(preset)}
	{#if note}
		<span class="max-w-[110px] truncate text-[11px] font-semibold opacity-65">{note}</span>
	{/if}
</button>
