import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import pandas as pd
df = pd.read_csv(r"{CSV_PATH}", parse_dates=['date'])
df = df.set_index('date')
monthly = df['value'].resample('MS').mean()
plt.figure()
plt.plot(monthly.index, monthly.values, marker='o')
plt.title('Monthly average value')
plt.xlabel('Month')
plt.ylabel('Value')
plt.savefig(r"{OUTPUT_PNG}", dpi=150, bbox_inches='tight')
