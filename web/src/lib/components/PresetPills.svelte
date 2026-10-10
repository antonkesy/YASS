<script lang="ts">
	import { bounceWhenPicked } from '$lib/motion';
	import { t } from '$lib/i18n/index.svelte';
	import type { Preset } from '$lib/presets';
	import { Icon } from '$lib/icons';
	import PresetPill from './PresetPill.svelte';

	let {
		presets,
		active,
		edited = null,
		modified,
		onpick,
		onsave
	}: {
		presets: Preset[];
		active: string | null;
		edited?: string | null;
		modified: boolean;
		onpick: (id: string) => void;
		onsave: () => void;
	} = $props();

	const bounce = bounceWhenPicked(() => active);
</script>

<div class="flex flex-col items-center gap-2">
	<!-- A fixed-height line, so the Save as preset link coming and going never rewraps the pills. -->
	<div class="grid h-8 w-full grid-cols-[1fr_auto_1fr] items-center gap-2">
		<span></span>
		<span class="text-[13px] font-semibold text-ink-muted">{t('home.preset')}</span>
		{#if modified}
			<button
				type="button"
				class="flex h-8 animate-fade-in items-center gap-1 justify-self-start rounded-full px-2 text-[12px] font-bold whitespace-nowrap text-accent transition-colors hover:bg-accent-wash"
				onclick={onsave}
				title={t('home.saveAsPresetHint')}
				data-testid="preset-save-current"
			>
				<Icon name="plus" />
				{t('home.saveAsPreset')}
			</button>
		{/if}
	</div>
	<div class="flex flex-wrap justify-center gap-1.5 rounded-[24px] bg-ctl-track p-[4px]">
		{#each presets as preset (preset.id)}
			<PresetPill
				{preset}
				active={active === preset.id}
				edited={edited === preset.id}
				editedTitle={t('home.presetEdited')}
				tray
				onclick={() => onpick(preset.id)}
				{@attach bounce(preset.id)}
			/>
		{/each}
	</div>
</div>
