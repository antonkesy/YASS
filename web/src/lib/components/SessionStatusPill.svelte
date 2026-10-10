<script lang="ts">
	import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { sessionStatus, statusLabels, type SessionStatus } from '$lib/format';

	let {
		session,
		size = 'inline'
	}: {
		session: SessionDto;
		size?: 'inline' | 'thumb';
	} = $props();

	const status = $derived(sessionStatus(session));

	// `failed` has no pill in the design, but showing a failed scan as a draft would lie.
	const tones: Record<SessionStatus, { chip: string; dot: string }> = {
		draft: { chip: 'bg-warning-bg text-warning-ink', dot: 'bg-warning' },
		scanning: { chip: 'bg-accent-wash2 text-accent-deep', dot: 'bg-accent animate-pulse-busy' },
		exporting: { chip: 'bg-accent-wash2 text-accent-deep', dot: 'bg-accent animate-pulse-busy' },
		exported: { chip: 'bg-success-bg2 text-success-ink', dot: 'bg-success' },
		saved: { chip: 'bg-saved-bg text-saved-ink', dot: 'bg-saved' },
		downloaded: { chip: 'bg-downloaded-bg text-accent-deep', dot: 'bg-downloaded' },
		discarded: { chip: 'bg-discarded-bg text-discarded-ink', dot: 'bg-discarded' },
		failed: { chip: 'bg-danger-bg text-danger', dot: 'bg-danger' }
	};
	const tone = $derived(tones[status]);
</script>

<span
	class="inline-flex flex-none items-center gap-1.5 rounded-full text-[10px] font-extrabold tracking-[0.02em] {tone.chip} {size ===
	'thumb'
		? 'h-[22px] px-2 shadow-seg'
		: 'h-5 px-[7px]'}"
	data-testid="session-status"
	data-status={status}
>
	<span
		class="rounded-full {tone.dot} {size === 'thumb' ? 'h-1.5 w-1.5' : 'h-1 w-1'}"
		aria-hidden="true"
	></span>
	{statusLabels[status]}
</span>
