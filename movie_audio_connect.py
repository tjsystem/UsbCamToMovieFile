import sys
import moviepy.editor as mp

wav_file = 'movie3.wav'
video_file = 'movie3.mp4'

audio = mp.AudioFileClip(wav_file)
video = mp.VideoFileClip(video_file)
video = video.set_audio(audio)
video.write_videofile('movie3_con.mp4')
