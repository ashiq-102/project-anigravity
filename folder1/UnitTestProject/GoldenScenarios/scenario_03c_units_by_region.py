import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import pandas as pd
df = pd.read_csv(r"{CSV_PATH}")
by_region = df.groupby('region', sort=False)['units'].sum()
plt.figure()
plt.bar(by_region.index, by_region.values)
plt.title('Units by region')
plt.xlabel('Region')
plt.ylabel('Units')
plt.savefig(r"{OUTPUT_PNG}", dpi=150, bbox_inches='tight')
