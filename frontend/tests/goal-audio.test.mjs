import assert from 'node:assert/strict';
import {stadiumAudioClip} from '../src/features/match3d/stadiumAudioClips.ts';
import {matchAudioCues} from '../src/features/match3d/matchAudioCues.ts';

const clip=stadiumAudioClip('goal',14.781333,1,9);
assert.equal(clip.offset,5.65,'skip the recorded pre-goal ambience');
assert.equal(clip.duration,6.35,'stop after the roar, before the recording returns to ambience');
assert.deepEqual(stadiumAudioClip('goal',14.781333,2,9),{offset:5.65,duration:3.175},'pitch changes output duration, never the source offset');
assert.deepEqual(stadiumAudioClip('kick',.7,1,.6),{offset:0,duration:.6},'foot contact keeps its original timing');
assert.deepEqual(stadiumAudioClip('goal',4,1,9),{offset:4,duration:0},'short/unexpected buffers cannot schedule a negative duration');
for(const type of ['goal','shootout_goal']) {
 const event={type,minute:2,second:120.25};
 assert.deepEqual(matchAudioCues(120.2,120.249,[event],[]),[],'never anticipate the goal');
 assert.deepEqual(matchAudioCues(120.249,120.251,[event],[]),[{type:'goal'}],'fire on crossing the same timestamp used by the match');
 assert.deepEqual(matchAudioCues(120.251,120.27,[event],[]),[],'never repeat the roar on following frames');
}
console.log('PASS: reação do gol sem os 5,65 s de introdução, janela/final corretos e disparo único no instante do gol e dos pênaltis.');
