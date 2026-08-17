import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import pandas as pd
df = pd.read_csv(r"{CSV_PATH}", nrows=900)
plt.figure()
plt.scatter(df['x'], df['y'], s=10, alpha=0.5)
plt.title('Scatter (first 900 rows)')
plt.xlabel('x')
plt.ylabel('y')
plt.savefig(r"{OUTPUT_PNG}", dpi=150, bbox_inches='tight')
