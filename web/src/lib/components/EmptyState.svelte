<script lang="ts">
	import { Icon } from '$lib/icons';

	let { title, body }: { title: string; body: string } = $props();
</script>

<div
	class="flex animate-fade-in flex-col items-center gap-2 rounded-[18px] bg-app px-3 py-9 text-center"
	data-testid="no-sessions"
>
	<span
		class="flex h-11 w-11 items-center justify-center rounded-[14px] bg-surface text-accent shadow-chip"
	>
		<Icon name="archive" />
	</span>
	<p class="text-[15px] font-extrabold">{title}</p>
	<p class="text-[13px] text-balance text-ink-muted">{body}</p>
</div>
